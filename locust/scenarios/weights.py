def get_scenario_weights(scenario_name):
    if scenario_name == "black_friday":
        return {
            "product_search": 60,
            "product_detail": 20,
            "basket_add": 15,
            "basket_remove": 0,
            "basket_get": 5
        }
    elif scenario_name == "heavy_basket":
        return {
            "product_search": 10,
            "product_detail": 10,
            "basket_add": 40,
            "basket_remove": 20,
            "basket_get": 20
        }
    else: # standard
        return {
            "product_search": 45,
            "product_detail": 35,
            "basket_add": 10,
            "basket_remove": 5,
            "basket_get": 5
        }
