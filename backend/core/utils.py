def parse_vendor_name(vendor_raw: str | None) -> str | None:
    """PLANT_VendorName_VendorCode 형식에서 VendorName 추출. 그 외 형식은 원본 반환."""
    if not vendor_raw:
        return None
    parts = vendor_raw.split('_')
    if len(parts) >= 3:
        return '_'.join(parts[1:-1])
    return vendor_raw
