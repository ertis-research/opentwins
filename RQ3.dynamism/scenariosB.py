from datetime import datetime, timezone
import logging
import pandas as pd
from rdflib import Literal, Namespace, URIRef
import use_platform
from dateutil.parser import isoparse

# ==================================================
# Scenario Block B: Dynamic reconfiguration placeholders
# ==================================================
TWIN_ID = "urn:test:rq3"
TERMINALB_ID = "urn:test:rq3:TerminalB"

def init_test():
    listId = []
    
    use_platform.remove_and_init_DB()
    
    listId.append(use_platform.post_thing("thingDescriptions/terminalA.json"))
    listId.append(use_platform.post_thing("thingDescriptions/terminalB.json"))
    listId.append(use_platform.post_thing("thingDescriptions/airport.json"))
    for i in range(1, 6):
        listId.append(use_platform.post_thing("thingDescriptions/plane.json", f"{TWIN_ID}:Plane" + str(i), f"Plane{i}"))
        
    use_platform.post_twin(TWIN_ID)
    use_platform.add_thing_to_twin(TWIN_ID, ",".join(listId))
    
    use_platform.add_link(f"{TWIN_ID}:Plane3", {
        "href": TERMINALB_ID,
        "rel": "departingFrom",
        "type": "application/td+json"
    })


def scenario_b1_create_thing_without_twin(kafka_client):
    init_test()
    g1 = use_platform.load_graph_from_api(TWIN_ID, "init_rq3_b1")
    PLANE_ID=f"{TWIN_ID}:Plane6"
    
    send_request_time = datetime.now(timezone.utc)
    logging.info(f"Sending request to create plane ID {PLANE_ID} at {send_request_time.isoformat()}")
    use_platform.post_thing("thingDescriptions/plane.json", PLANE_ID)
    
    result = kafka_client.get_time_from_source(topic="thing.description.changes", source=PLANE_ID, start_timestamp=send_request_time.timestamp()*1000)
    latency = float("nan")
    if result != None:
        latency = (result - send_request_time).total_seconds()
        logging.info(f"Received creation for plane ID {PLANE_ID}. Latency: {latency:.3f} seconds")
    
    g2 = use_platform.load_graph_from_api(TWIN_ID, "after_rq3_b1")
    
    match = g1.isomorphic(g2)
    
    return pd.DataFrame([{
        "scenario": "B1_create_thing",
        "iteration": 1,
        "match": match,
        "latency": latency,
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }])


def scenario_b2_add_thing_to_twin():
    init_test()
    PLANE_ID=f"{TWIN_ID}:Plane7"
    
    g1 = use_platform.load_graph_from_api(TWIN_ID, "init_rq3_b2")
    use_platform.post_thing("thingDescriptions/plane.json", PLANE_ID)

    send_request_time = datetime.now(timezone.utc)
    logging.info(f"Sending request to add plane ID {PLANE_ID} to twin {TWIN_ID} at {send_request_time.isoformat()}")
    use_platform.add_thing_to_twin(TWIN_ID, PLANE_ID)
    
    g2 = use_platform.load_graph_from_api(TWIN_ID, "after_rq3_b2")

    plane_uri = URIRef(PLANE_ID)
    createdAt = None
    for s, p, o in g2.triples((plane_uri, None, None)):
        if str(p).endswith("createdAt"):
            if isinstance(o, Literal):
                createdAt = isoparse(str(o))
            break

    if createdAt:
        latency = (createdAt - send_request_time).total_seconds()
        logging.info(f"Plane node {PLANE_ID} created at {createdAt}, latency: {latency:.3f} s")
    else:
        latency = float("nan")
        logging.warning(f"Node {PLANE_ID} missing createdAt. Latency set to NaN.")

    
    plane_uri = URIRef(PLANE_ID)
    # Verificar match: g2 = g1 + un nodo PLANE_ID
    g1_nodes = set(g1.subjects())
    g2_nodes = set(g2.subjects())
    match = (plane_uri in g2_nodes) and (g2_nodes - g1_nodes == {plane_uri})
    logging.info(f"Match {match}")
    
    return pd.DataFrame([{
        "scenario": "B2_add_thing_to_twin",
        "iteration": 1,
        "match": match,
        "latency": latency,
        "timestamp": datetime.now(timezone.utc).isoformat()
    }])


def scenario_b3_delete_thing():
    """Placeholder for scenario: delete thing and verify removal."""
    pass


def scenario_b4_add_relationship(kafka_client):
    init_test()
    g1 = use_platform.load_graph_from_api(TWIN_ID, "init_rq3_b4")
    PLANE_ID=f"{TWIN_ID}:Plane2"
    TERMINAL_ID = f"{TWIN_ID}:TerminalA"
    NS1 = Namespace("http://example.org/")
    
    data = {
        "href": TERMINAL_ID,
        "rel": "location",
        "type": "application/td+json"
    }
    
    send_request_time = datetime.now(timezone.utc)
    logging.info(f"Sending request to create plane ID {PLANE_ID} at {send_request_time.isoformat()}")
    use_platform.add_link(PLANE_ID, data)
    
    result = kafka_client.get_time_from_source(topic="thing.description.changes", source=PLANE_ID, start_timestamp=send_request_time.timestamp()*1000)
    latency = float("nan")
    if result != None:
        latency = (result - send_request_time).total_seconds()
        logging.info(f"Received modification for plane ID {PLANE_ID}. Latency: {latency:.3f} seconds")
    
    g2 = use_platform.load_graph_from_api(TWIN_ID, "after_rq3_b4")
    
    plane_uri = URIRef(PLANE_ID)
    terminal_uri = URIRef(TERMINAL_ID)
    location_uri = NS1.location

    g1_triples = set(g1)
    g2_triples = set(g2)
    location_triple_1 = (plane_uri, location_uri, terminal_uri)
    location_triple_2 = (terminal_uri, location_uri, plane_uri)

    extra_triples = g2_triples - g1_triples
    expected_extra = {location_triple_1, location_triple_2}

    match = (
        g1_triples.issubset(g2_triples) and
        expected_extra.issubset(extra_triples) and
        extra_triples.issubset(expected_extra)
    )
    logging.info(f"Match {match}")
    
    return pd.DataFrame([{
        "scenario": "B4_add_rel",
        "iteration": 1,
        "match": match,
        "latency": latency,
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }])


def scenario_b5_modify_relationship(kafka_client):
    init_test()
    g1 = use_platform.load_graph_from_api(TWIN_ID, "init_rq3_b5")
    PLANE_ID=f"{TWIN_ID}:Plane3"
    NS1 = Namespace("http://example.org/")
    
    data = {
        "href": TERMINALB_ID,
        "rel": "arrivingAt",
        "type": "application/td+json"
    }
    
    send_request_time = datetime.now(timezone.utc)
    logging.info(f"Sending request to create plane ID {PLANE_ID} at {send_request_time.isoformat()}")
    use_platform.update_link(PLANE_ID, TERMINALB_ID, "departingFrom", data)
    
    result = kafka_client.get_time_from_source(topic="thing.description.changes", source=PLANE_ID, start_timestamp=send_request_time.timestamp()*1000)
    latency = float("nan")
    if result != None:
        latency = (result - send_request_time).total_seconds()
        logging.info(f"Received modification for plane ID {PLANE_ID}. Latency: {latency:.3f} seconds")
    
    g2 = use_platform.load_graph_from_api(TWIN_ID, "after_rq3_b5")

    plane_uri = URIRef(PLANE_ID)
    terminal_uri = URIRef(TERMINALB_ID)
    departing_uri = NS1.departingFrom
    arriving_uri = NS1.arrivingAt

    g1_triples = set(g1)
    g2_triples = set(g2)
    removed_trip = (plane_uri, departing_uri, terminal_uri) # Debe haber eliminado la relación departingFrom
    new_trip = (plane_uri, arriving_uri, terminal_uri) # Debe existir la nueva relación arrivingAt

    extra_triples = g2_triples - g1_triples
    match = removed_trip not in g2_triples and new_trip in g2_triples and all(
        t in g2_triples or t == removed_trip for t in g1_triples
    ) and extra_triples.issubset({new_trip})
    
    logging.info(f"Match {match}")
    
    return pd.DataFrame([{
        "scenario": "B5_modify_rel",
        "iteration": 1,
        "match": match,
        "latency": latency,
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }])


def scenario_b6_delete_relationship(kafka_client):
    init_test()
    g1 = use_platform.load_graph_from_api(TWIN_ID, "init_rq3_b6")
    PLANE_ID=f"{TWIN_ID}:Plane3"
    NS1 = Namespace("http://example.org/")
    
    send_request_time = datetime.now(timezone.utc)
    logging.info(f"Sending request to delete link plane ID {PLANE_ID} at {send_request_time.isoformat()}")
    use_platform.delete_link(PLANE_ID, TERMINALB_ID, "departingFrom")
    
    result = kafka_client.get_time_from_source(topic="thing.description.changes", source=PLANE_ID, start_timestamp=send_request_time.timestamp()*1000)
    latency = float("nan")
    if result != None:
        latency = (result - send_request_time).total_seconds()
        logging.info(f"Received link deletion for plane ID {PLANE_ID}. Latency: {latency:.3f} seconds")
    
    g2 = use_platform.load_graph_from_api(TWIN_ID, "after_rq3_b6")

    plane_uri = URIRef(PLANE_ID)
    terminal_uri = URIRef(TERMINALB_ID)
    departing_uri = NS1.departingFrom
    
    g1_triples = set(g1)
    g2_triples = set(g2)
    removed_triple = (plane_uri, departing_uri, terminal_uri)
    removed_triples = g1_triples - g2_triples
    extra_triples = g2_triples - g1_triples

    match = (
        removed_triple in removed_triples and  # se eliminó el triple correcto
        removed_triples == {removed_triple} and  # no se eliminó nada más
        len(extra_triples) == 0  # no se añadió nada nuevo
    )
    
    logging.info(f"Match {match}")
    
    return pd.DataFrame([{
        "scenario": "B6_delete_rel",
        "iteration": 1,
        "match": match,
        "latency": latency,
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }])