import re
import os
import polars as pl
from pathlib import Path

class ParseError(Exception):
    pass

def _extract_model_info(file_path: str) -> tuple[str, str]:
    """
    파일명에서 (model_code, suffix) 추출.
    'LSGL6335X.ARSELGA@CVZ.EKHQ 1.0.xlsx' → ('LSGL6335X', 'ARSELGA')
    """
    filename = os.path.basename(file_path)
    # Remove prefix if exists (e.g. 20260426_015139_LSGL...)
    m = re.match(r"\d{8}_\d{6}_(.*)", filename)
    actual_name = m.group(1) if m else filename
    
    parts = actual_name.split(".")
    model_code = parts[0].split("_")[0]
    suffix = parts[1].split("@")[0] if len(parts) > 1 else ""
    return model_code, suffix

def _parse_level(lvl_str: str) -> int:
    """
    Lvl 문자열에서 레벨 정수 추출.
    '0' → 0, '.1' → 1, '..2' → 2, '*S*' → -1 (대체품 표시)
    """
    if not isinstance(lvl_str, str):
        lvl_str = str(lvl_str)
    lvl_str = lvl_str.strip()
    if lvl_str == '*S*':
        return -1
    # Count the dots by subtracting the length without leading dots
    # This correctly parses '0', '.1', '..2', etc.
    return len(lvl_str) - len(lvl_str.lstrip('.'))

def _compute_paths(levels: list[int]) -> list[str]:
    """
    레벨 배열에서 materialized path 배열을 계산한다.
    예: [0, 1, 2, 2, 1, 2] → ['0', '0.1', '0.1.2', '0.1.3', '0.4', '0.4.5']
    루트(level=0)는 '0'으로 시작.
    """
    paths = []
    current_path = []
    
    for level in levels:
        if level < 0:
            # Handle substitute (-1) or invalid level gracefully.
            # Substitutes share the level of their parent context.
            paths.append(".".join(map(str, current_path)) + ".S")
            continue
            
        if not current_path:
            # If the tree starts without a 0, we still want to establish a base
            # Typically first item is level 0, but we handle robustly.
            current_path = [0]
        else:
            if level > len(current_path) - 1:
                # Go deeper
                while len(current_path) <= level:
                    current_path.append(1)
            elif level == len(current_path) - 1:
                # Same level
                current_path[-1] += 1
            else:
                # Go up
                current_path = current_path[:level + 1]
                current_path[-1] += 1
        
        paths.append(".".join(map(str, current_path)))
    
    return paths

def _deduplicate(df: pl.DataFrame) -> pl.DataFrame:
    """
    동일 (parent_part_number, part_number, level) 그룹에서
    R='P' 행이 있으면 R='P' 유지, 없으면 R='B' 유지.
    R='S' (대체품)는 is_substitute=True 플래그로 유지하되 트리에서 제외 옵션 제공.
    """
    # Create an ordering column to prioritize P over B
    df = df.with_columns([
        pl.when(pl.col("row_type") == "P").then(0)
        .when(pl.col("row_type") == "B").then(1)
        .otherwise(2).alias("_row_priority")
    ])
    
    # Fill null parent_part_number with empty string for grouping
    df = df.with_columns(
        pl.col("parent_part_number").fill_null("")
    )
    
    # Sort by group and priority, then keep the first within each group
    # Note: R='S' rows will have their own uniqueness if they are duplicates, but we keep them unless deduplicated away
    
    # We must maintain original tree order. 
    # Let's add original_row_id to preserve the tree structure after deduplication.
    df = df.with_columns(pl.arange(0, df.height).alias("_original_id"))
    
    df = df.sort(["parent_part_number", "part_number", "level", "_row_priority"])
    df = df.unique(subset=["parent_part_number", "part_number", "level"], keep="first", maintain_order=False)
    
    # Restore original tree order
    df = df.sort("_original_id")
    
    # Restore parent_part_number nulls if desired or leave as empty string
    df = df.with_columns(
        pl.when(pl.col("parent_part_number") == "").then(pl.lit(None)).otherwise(pl.col("parent_part_number")).alias("parent_part_number")
    ).drop("_row_priority")
    
    return df

def parse(file_path: str) -> pl.DataFrame:
    """
    BOM Excel 파일을 정규화된 Polars DataFrame으로 변환한다.
    """
    import fastexcel
    
    basename = os.path.basename(file_path)
    try:
        model_code, suffix = _extract_model_info(file_path)
    except Exception as e:
        raise ParseError(f"Cannot extract model_code from filename: {basename}")
        
    try:
        doc = fastexcel.read_excel(file_path)
        # 0-indexed: 1 is Row 2 in Excel, which is the data start.
        # Header is Row 1 (index 0). By default fastexcel uses first row as header.
        # The prompt says: "시트명: ag-grid", "헤더 행: Row 1 (0-indexed: 0)"
        df = doc.load_sheet("ag-grid").to_polars()
    except Exception as e:
        raise ParseError(f"Failed to read excel file: {e}")
        
    if df.height == 0:
        raise ParseError("BOM file contains no data rows")

    cols = df.columns
    # Check required columns by index as specified (or by name robustly)
    # The prompt provided specific mapping indices. We can use names to be safe but index fallback
    col_map = {}
    
    # Find matching columns by header names (case/space insensitive robust match)
    for c in cols:
        cl = c.lower().replace(" ", "").replace("'", "").replace("(모)", "").replace("(자)", "")
        if cl in ["lvl", "level"]: col_map["lvl_str"] = c
        elif cl in ["parentpartno"]: col_map["parent_part_number"] = c
        elif cl in ["partname"]: col_map["part_name"] = c
        elif cl in ["description"]: col_map["description"] = c
        elif cl in ["partno"]: col_map["part_number"] = c
        elif cl in ["qty"]: col_map["qty"] = c
        elif cl in ["uom"]: col_map["uom"] = c
        elif cl in ["companyname", "vendor", "maker"]: 
            if "vendor_raw" not in col_map: col_map["vendor_raw"] = c
        elif cl in ["r"]: col_map["row_type"] = c
        elif cl in ["supplytype"]: col_map["supply_type"] = c
        elif cl in ["makercode"]: col_map["maker_code"] = c

    # Fallback to index based if name matching failed for crucial columns
    if "lvl_str" not in col_map and len(cols) > 1: col_map["lvl_str"] = cols[1]
    if "part_number" not in col_map and len(cols) > 5: col_map["part_number"] = cols[5]
    if "qty" not in col_map and len(cols) > 6: col_map["qty"] = cols[6]
    if "uom" not in col_map and len(cols) > 7: col_map["uom"] = cols[7]
    if "row_type" not in col_map and len(cols) > 13: col_map["row_type"] = cols[13]

    req_keys = ["lvl_str", "part_number", "qty", "uom", "row_type"]
    for rk in req_keys:
        if rk not in col_map:
            raise ParseError(f"Required column '{rk}' not found. File: {file_path}")

    # Select and rename columns we need
    select_exprs = []
    for k, v in col_map.items():
        select_exprs.append(pl.col(v).alias(k))
    
    df = df.select(select_exprs)
    
    # Filter rows with null part_number
    df = df.filter(pl.col("part_number").is_not_null())
    
    # Process level
    df = df.with_columns([
        pl.col("lvl_str").map_elements(_parse_level, return_dtype=pl.Int32).alias("level"),
        pl.col("row_type").cast(pl.Utf8).fill_null("B"),
        pl.col("qty").cast(pl.Float64, strict=False).fill_null(1.0)
    ])
    
    # Filter rows with qty > 0
    df = df.filter(pl.col("qty") > 0)
    
    # Set is_substitute
    df = df.with_columns(
        (pl.col("row_type") == "S").alias("is_substitute")
    )
    
    # Add optional columns if they don't exist
    for opt_col in ["parent_part_number", "part_name", "description", "vendor_raw", "maker_code", "supply_type"]:
        if opt_col not in df.columns:
            df = df.with_columns(pl.lit(None).cast(pl.Utf8).alias(opt_col))
    
    # Cast to string
    str_cols = ["part_number", "part_name", "description", "uom", "vendor_raw", "maker_code", "supply_type", "parent_part_number", "row_type"]
    df = df.with_columns([pl.col(c).cast(pl.Utf8) for c in str_cols])
    
    # Deduplicate
    df = _deduplicate(df)
    
    # Compute paths
    levels = df["level"].to_list()
    paths = _compute_paths(levels)
    
    # Add final columns
    df = df.with_columns([
        pl.lit(model_code).alias("model_code"),
        pl.lit(suffix).alias("suffix"),
        pl.arange(0, df.height).alias("sort_order"),
        pl.Series(name="path", values=paths)
    ])
    
    return df.select([
        "model_code", "suffix", "level", "part_number", "part_name", "description", 
        "qty", "uom", "vendor_raw", "maker_code", "supply_type", "parent_part_number",
        "row_type", "is_substitute", "sort_order", "path"
    ])
