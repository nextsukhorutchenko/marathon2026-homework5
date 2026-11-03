"""Create a small, durable public demo catalogue without overwriting any record.

Run with the existing AWS profile and boto3 (available in the Syndicate venv).
These records are application demo data, not disposable aqa-* test fixtures.
"""
import json

import boto3
from boto3.dynamodb.types import TypeSerializer


ACCOUNT = "009160045555"
REGION = "us-west-2"
MARKER = "level3-menu-v1"
LOCATION = "level3-demo-green-tasty"


def catalogue():
    dishes = [
        ("spring-salad", "Spring Salad", 0, 12, "spring-salad.jpg"),
        ("strawberry-salad", "Fresh Strawberry & Mint Salad", 0, 14, "fresh-strawberry-mint-salad.jpg"),
        ("lentil-salad", "Roasted Sweet Potato and Lentil Salad", 1, 18, "roasted-sweet-potato-and-lentil-salad.jpg"),
        ("avocado-bowl", "Avocado Pine Nut Bowl", 1, 16, "avocado-pine-nut-bowl.jpg"),
        ("lemon-pasta", "Lemon Herb Pasta", 1, 20, None),
        ("berry-dessert", "Berry Dessert", 2, 10, None),
    ]
    rows = [("Locations", {
        "id": LOCATION, "address": "1 Green Street, Demo City",
        "description": "Fictional restaurant for the EPAM homework demonstration.",
        "totalCapacity": 10, "averageOccupancy": "0%", "rating": 0,
        "imageUrl": "assets/img/restaurant.png",
        "specialtyDishIds": ["level3-demo-spring-salad", "level3-demo-avocado-bowl"],
    })]
    for number, capacity in [(1, 4), (2, 6)]:
        rows.append(("Tables", {"id": f"level3-demo-table-{number}", "locationId": LOCATION,
                                "tableNumber": number, "capacity": capacity}))
    for slug, name, dish_type, price, photo in dishes:
        rows.append(("Dishes", {
            "id": f"level3-demo-{slug}", "name": name, "dishType": dish_type,
            "description": f"{name}. Demonstration menu item; nutrition values are sample data.",
            "price": price, "weight": "250 g", "state": 1, "locationId": LOCATION,
            "imageUrl": f"assets/img/dishes/{photo}" if photo else "assets/img/dish.png",
            "calories": "250", "proteins": "8", "fats": "10", "carbohydrates": "30", "vitamins": "N/A",
        }))
    return [(table, {**item, "catalogSeed": MARKER}) for table, item in rows]


def main():
    session = boto3.Session(region_name=REGION)
    if session.client("sts").get_caller_identity()["Account"] != ACCOUNT:
        raise RuntimeError("AWS account mismatch; no writes performed")
    database = session.resource("dynamodb")
    serializer = TypeSerializer()
    missing = []
    rows = catalogue()
    for table, item in rows:
        existing = database.Table(table).get_item(Key={"id": item["id"]}, ConsistentRead=True).get("Item")
        if existing is not None and existing != item:
            raise RuntimeError(f"Existing record differs: {table}/{item['id']}; no writes performed")
        if existing is None:
            missing.append({"Put": {"TableName": table,
                "Item": {key: serializer.serialize(value) for key, value in item.items()},
                "ConditionExpression": "attribute_not_exists(id)"}})
    if missing:
        session.client("dynamodb").transact_write_items(TransactItems=missing)
    for table, item in rows:
        actual = database.Table(table).get_item(Key={"id": item["id"]}, ConsistentRead=True).get("Item")
        if actual != item:
            raise RuntimeError(f"Read-back mismatch: {table}/{item['id']}")
    print(json.dumps({"marker": MARKER, "created": len(missing), "verified": len(rows),
                      "records": [{"table": table, "id": item["id"]} for table, item in rows]}, indent=2))


if __name__ == "__main__":
    main()
