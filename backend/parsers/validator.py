import polars as pl
from dataclasses import dataclass
from typing import List

@dataclass
class RowError:
    row_index: int
    column: str
    message: str

@dataclass
class ValidationResult:
    is_valid: bool
    errors: List[RowError]
    valid_row_count: int
    invalid_row_count: int

def validate_bom(df: pl.DataFrame) -> ValidationResult:
    errors = []
    invalid_rows = set()
    
    for idx, row in enumerate(df.iter_rows(named=True)):
        if row.get("part_number") is None:
            errors.append(RowError(idx, "part_number", "Part number cannot be null"))
            invalid_rows.add(idx)
        if row.get("level") is None or row.get("level") < -1:
            errors.append(RowError(idx, "level", "Level must be >= -1"))
            invalid_rows.add(idx)
        if row.get("qty") is None or row.get("qty") <= 0:
            errors.append(RowError(idx, "qty", "Quantity must be > 0"))
            invalid_rows.add(idx)
            
    valid_count = df.height - len(invalid_rows)
    
    return ValidationResult(
        is_valid=len(errors) == 0,
        errors=errors,
        valid_row_count=valid_count,
        invalid_row_count=len(invalid_rows)
    )

def validate_daily_plan(df: pl.DataFrame) -> ValidationResult:
    errors = []
    invalid_rows = set()
    
    for idx, row in enumerate(df.iter_rows(named=True)):
        if row.get("model_code") is None:
            errors.append(RowError(idx, "model_code", "Model code cannot be null"))
            invalid_rows.add(idx)
        if row.get("planned_qty") is None or row.get("planned_qty") < 0:
            errors.append(RowError(idx, "planned_qty", "Planned quantity must be >= 0"))
            invalid_rows.add(idx)
            
    valid_count = df.height - len(invalid_rows)
    
    return ValidationResult(
        is_valid=len(errors) == 0,
        errors=errors,
        valid_row_count=valid_count,
        invalid_row_count=len(invalid_rows)
    )
