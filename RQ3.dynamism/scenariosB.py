import use_platform

# ==================================================
# Scenario Block B: Dynamic reconfiguration placeholders
# ==================================================
ID_TWIN = "urn:test:rq3"

def init_test():
    listId = []
    
    use_platform.remove_and_init_DB()
    
    listId.append(use_platform.post_thing("thingDescriptions/terminalA.json"))
    listId.append(use_platform.post_thing("thingDescriptions/terminalB.json"))
    listId.append(use_platform.post_thing("thingDescriptions/airport.json"))
    for i in range(1, 6):
        listId.append(use_platform.post_thing("thingDescriptions/plane.json", "urn:test:rq3:Plane" + str(i), f"Plane{i}"))
        
    use_platform.post_twin(ID_TWIN)
    use_platform.add_thing_to_twin(ID_TWIN, ",".join(listId))

def scenario_b1_create_thing_without_twin():
    """Placeholder for scenario: create thing not linked to twin."""
    pass


def scenario_b2_add_thing_to_twin():
    """Placeholder for scenario: link thing to twin and verify visibility."""
    pass


def scenario_b3_delete_thing():
    """Placeholder for scenario: delete thing and verify removal."""
    pass


def scenario_b4_add_relationship():
    """Placeholder for scenario: add relationship between things."""
    pass


def scenario_b5_modify_relationship():
    """Placeholder for scenario: modify existing relationship."""
    pass


def scenario_b6_delete_relationship():
    """Placeholder for scenario: delete relationship."""
    pass