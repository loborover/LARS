from typing import List, Tuple, Dict, Any, Optional
import polars as pl
import os
import re
from datetime import datetime, date

class ParseError(Exception):
    pass

def parse_excel(file_path: str) -> pl.DataFrame:
    """
    Excel_Export_[MMDD_hhmmss].xlsx 형식 DP 파일 파싱.
    """
    import fastexcel
    
    try:
        doc = fastexcel.read_excel(file_path)
        df_raw = doc.load_sheet(0).to_polars()
    except Exception as e:
        raise ParseError(f"Failed to read excel file: {e}")

    # Find the header row by looking for 'W/O 계획수량'
    header_idx = -1
    for i, row in enumerate(df_raw.iter_rows()):
        if any(isinstance(x, str) and "W/O 계획수량" in x for x in row):
            header_idx = i
            break
            
    if header_idx == -1:
        raise ParseError("Cannot find header row with 'W/O 계획수량'")
        
    row_headers = df_raw.row(header_idx)
    new_cols = {}
    for i, col in enumerate(df_raw.columns):
        val = row_headers[i]
        if val is not None and isinstance(val, str):
            val = val.strip()
            # If multiple columns have the same name in the header row, keep the first one or specific logic
            # Polars doesn't like duplicate columns in rename
            if val == "W/O 계획수량": target = "planned_qty"
            elif val == "W/O Input": target = "input_qty"
            elif val == "W/O실적" or val == "W/O완료": target = "output_qty"
            elif val == "W/O": target = "wo_number"
            elif val == "모델" or val == "Model" or val == "품명코드": target = "model_code"
            elif val == "Lot Code" or val == "Lot No": target = "lot_number"
            elif val == "Planned Start Time": target = "planned_start_str"
            elif val == "생산 라인" or val == "Line": target = "line_code"
            elif val == "Suffix": target = "suffix"
            elif val == "Production Start Date": target = "production_start_str"
            else: target = None
            
            if target and target not in new_cols.values():
                new_cols[col] = target
                
    if "model_code" not in new_cols.values() or "planned_qty" not in new_cols.values():
        raise ParseError("Required columns not found in daily plan.")
        
    df = df_raw.rename({k: v for k, v in new_cols.items() if v in [
        "planned_qty", "input_qty", "output_qty", "wo_number", "model_code", 
        "lot_number", "planned_start_str", "line_code", "suffix", "production_start_str"
    ]})
    
    # Slice to data rows (from header_idx + 2 because row after header is usually a sum row)
    df = df.slice(header_idx + 2)
    
    # Filter valid rows (no null W/O or model code)
    if "wo_number" in df.columns:
        df = df.filter(pl.col("wo_number").is_not_null())
    df = df.filter(pl.col("model_code").is_not_null())
    
    if df.height == 0:
        raise ParseError("No valid data rows found in excel.")
        
    # Extract date logic
    filename = os.path.basename(file_path)
    m = re.search(r"\[(\d{4})_(\d{6})\]", filename)
    file_year = "2026"
    if "planned_start_str" in df.columns:
        df = df.with_columns(pl.col("planned_start_str").cast(pl.Utf8).str.strptime(pl.Datetime, "%Y-%m-%d %H:%M:%S", strict=False).alias("planned_start"))
        first_date = df.select("planned_start").drop_nulls().head(1)
        if first_date.height > 0:
            file_year = str(first_date[0, 0].year)
            
    # Plan Date
    if "production_start_str" in df.columns:
        df = df.with_columns(pl.col("production_start_str").cast(pl.Utf8).str.strptime(pl.Date, "%Y-%m-%d", strict=False).alias("plan_date"))
    elif "planned_start" in df.columns:
        df = df.with_columns(pl.col("planned_start").cast(pl.Date).alias("plan_date"))
    else:
        df = df.with_columns(pl.lit(date(2026, 1, 1)).alias("plan_date"))
        
    df = df.filter(pl.col("plan_date").is_not_null())
    
    # Defaults and casts
    df = df.with_columns([
        pl.arange(0, df.height).alias("sort_order"),
        pl.col("planned_qty").cast(pl.Float64).cast(pl.Int32, strict=False).fill_null(0),
        pl.col("input_qty").cast(pl.Float64).cast(pl.Int32, strict=False).fill_null(0) if "input_qty" in df.columns else pl.lit(0).alias("input_qty"),
        pl.col("output_qty").cast(pl.Float64).cast(pl.Int32, strict=False).fill_null(0) if "output_qty" in df.columns else pl.lit(0).alias("output_qty"),
        pl.col("model_code").cast(pl.Utf8),
        pl.col("wo_number").cast(pl.Utf8),
        pl.col("line_code").cast(pl.Utf8),
        pl.col("suffix").cast(pl.Utf8) if "suffix" in df.columns else pl.lit("").alias("suffix"),
    ])
    
    if "lot_number" not in df.columns:
        df = df.with_columns(pl.lit("N/A").alias("lot_number"))
        
    # Return matched API expected signature: Tuple[str, str, df] previously, but now instructions say return df only?
    # "두 포맷 모두 동일한 컬럼 구조 반환 (daily_qty_json은 CSV에만 있음)."
    # Also we had parse return tuple. Let's adapt route if needed or adapt here.
    return df

def parse_csv(file_path: str) -> pl.DataFrame:
    try:
        # Polars read_csv expands globs by default. Brackets in filenames cause issues.
        # We can read into bytes first or disable globbing (which might not be exposed directly in read_csv)
        with open(file_path, "rb") as f:
            df_raw = pl.read_csv(f.read(), try_parse_dates=False, truncate_ragged_lines=True)
    except Exception as e:
        raise ParseError(f"Failed to read CSV: {e}")
        
    # Standardize column names
    col_map = {}
    date_cols = []
    for c in df_raw.columns:
        cl = c.strip().lower()
        if cl == "line": col_map[c] = "line_code"
        elif cl == "demand id": col_map[c] = "wo_number"
        elif cl == "model": col_map[c] = "model_code"
        elif cl == "suffix": col_map[c] = "suffix"
        elif cl == "pst": col_map[c] = "planned_start_str"
        elif cl == "lot qty": col_map[c] = "planned_qty"
        elif cl == "result qty": col_map[c] = "output_qty"
        elif re.match(r"\d{2}/\d{2}", c.strip()):
            date_cols.append(c)
            
    df = df_raw.rename(col_map)
    
    req_cols = ["line_code", "wo_number", "model_code", "planned_qty", "planned_start_str"]
    for rc in req_cols:
        if rc not in df.columns:
            raise ParseError(f"CSV missing required column: {rc}")
            
    # Filter rows: empty demand id or 'Sub-total' rows
    df = df.filter(pl.col("wo_number").is_not_null() & (pl.col("wo_number").cast(pl.Utf8).str.strip_chars() != ""))
    
    # Process dates
    df = df.with_columns([
        pl.col("planned_start_str").str.strptime(pl.Datetime, "%Y-%m-%d %H:%M:%S", strict=False).alias("planned_start")
    ])
    df = df.with_columns([
        pl.col("planned_start").cast(pl.Date).alias("plan_date")
    ])
    df = df.filter(pl.col("plan_date").is_not_null())
    
    # Process JSON for dates
    if date_cols:
        # Compute year from planned_start
        df = df.with_columns(pl.col("planned_start").dt.year().alias("_year"))
        
        # Build JSON dictionary struct
        json_struct_exprs = []
        for dc in date_cols:
            mm, dd = dc.split("/")
            # construct full date string: YYYY-MM-DD
            full_date_col = pl.concat_str([pl.col("_year"), pl.lit(f"-{mm}-{dd}")]).alias(dc)
            # Create a struct of {full_date: val}
            # Simplest way in polars without complex mapping: just construct a JSON string manually or use dicts
            pass
            
        # Due to polars string building limits, a simpler way is applying a function
        def make_json(row):
            import json
            j = {}
            yr = row["_year"]
            for dc in date_cols:
                val = row[dc]
                if val is not None and val != "":
                    try:
                        v = float(val)
                        mm, dd = dc.split("/")
                        j[f"{yr}-{mm}-{dd}"] = v
                    except:
                        pass
            return json.dumps(j)
            
        json_series = pl.Series([make_json(r) for r in df.to_dicts()])
        df = df.with_columns(json_series.alias("daily_qty_json"))
        df = df.drop("_year")
    else:
        df = df.with_columns(pl.lit("{}").alias("daily_qty_json"))
        
    df = df.with_columns([
        pl.arange(0, df.height).alias("sort_order"),
        pl.col("planned_qty").cast(pl.Float64).cast(pl.Int32, strict=False).fill_null(0),
        pl.col("output_qty").cast(pl.Float64).cast(pl.Int32, strict=False).fill_null(0) if "output_qty" in df.columns else pl.lit(0).alias("output_qty"),
        pl.lit(0).alias("input_qty"),
        pl.col("model_code").cast(pl.Utf8),
        pl.col("wo_number").cast(pl.Utf8),
        pl.col("line_code").cast(pl.Utf8),
        pl.col("suffix").cast(pl.Utf8),
    ])
    
    return df

def parse(file_path: str) -> pl.DataFrame:
    """
    파일 확장자에 따라 parse_excel 또는 parse_csv 자동 선택.
    두 포맷 모두 동일한 컬럼 구조 반환.
    """
    if file_path.lower().endswith('.csv'):
        return parse_csv(file_path)
    else:
        return parse_excel(file_path)
