1. 원본 BOM은 트리구조임. BOM Amount 쿼리를 작성해서 각 모델당 실제 item 소요가 얼마인지 Required Qty를 구한 값을 열거한 Table이 필요함. 아마 Backend에서 필요할듯.
2. BOM Amount가 있으면 DailyPlan의 Model 열과, BOM Amount의 Model.suffix 열을 join해서 각 Lot별 자재 소요량이 얼마인지 확인할 수 있을 듯.
3. DailyPlan 하위탭으로 - View - Print 이렇게 만들어서 웹뷰용, 프린트용 두개로 나누어서 볼 수 있도록 해줘. 당연히 해당 기능들을 사용할때 하나의 파일을 가리켜서 사용할 수 있어야겠어.
4. 