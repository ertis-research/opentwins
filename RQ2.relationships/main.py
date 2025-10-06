#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
RQ2 - To what extent can KGs provide a more expressive means of representing and reasoning about the relationships among CDTs?
"""

import requests
from rdflib import Graph
from dotenv import load_dotenv
import os
import init
import querys
import time

load_dotenv()

THINGS_ENDPOINT = os.getenv("OTV2_THINGS_URL")
TWINS_ENDPOINT = os.getenv("OTV2_TWINS_URL")
RDF_FORMAT = "nquads"

def set_property(thingId, property, value):
    data = {property: value}
    resp = requests.put(f"{THINGS_ENDPOINT}/things/{thingId}/state", json=data)
    try:
        resp.raise_for_status()
        #print("[INFO] Successfully sent")
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    return resp

def setPlane(thingId, targetId):
    data = {
            "href": targetId,
            "rel": "location",
            "type": "application/td+json"
        }
    resp = requests.put(f"{THINGS_ENDPOINT}/things/{thingId}/links", json=data)
    try:
        resp.raise_for_status()
        #print("[INFO] Successfully sent")
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    return resp

def removePlane(thingId, targetId):
    resp = requests.delete(f"{THINGS_ENDPOINT}/things/{thingId}/links/{targetId}")
    try:
        resp.raise_for_status()
        #print("[INFO] Successfully sent")
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    return resp

def load_graph_from_api(name):
    #print("Loading graph from API…")
    headers = {"Accept": "application/n-quads"}
    resp = requests.get(TWINS_ENDPOINT + "/twins/urn:test:rq2", headers=headers)
    resp.raise_for_status()

    g = Graph()
    g.parse(data=resp.text, format=RDF_FORMAT)
    g.serialize(f"{name}.ttl", format="turtle")
    #g.serialize(f"{name}.jsonld", format="json-ld")
    #print(f"Graph loaded with {len(g)} triples")
    if len(g) == 0:
        print("[ERROR] Graph empty")
    return g



def main():
    
    # ============================================
    # Initialize base configuration for the Digital Twin
    # ============================================
    print("Initializing base configuration for the DT environment...")
    init.prepare_base()
    
    
    # ============================================
    # Scenario 1 & 2: Hierarchical relationships between gates
    # Shows KG reasoning over structural dependencies (hierarchy)
    # ============================================
    print("\nHierarchical relationships between gates")
    print("------------------------------------------------------------")
    
    print("\nScenario 1: Setting initial occupancy of all gates to occupied except GateB2...")
    print("Expected: NOT COLLAPSED")
    set_property("urn:test:rq2:GateA1", "occupied", True)
    set_property("urn:test:rq2:GateA2", "occupied", True)
    set_property("urn:test:rq2:GateB1", "occupied", True)
    set_property("urn:test:rq2:GateB2", "occupied", False)
    g = load_graph_from_api("esc1")
    querys.check_hierarchy(g)
    
    # Update occupancy to simulate dynamic change
    print("\nScenario 2: Updating GateB2 to occupied and re-validating hierarchy...")
    print("Expected: COLLAPSED")
    set_property("urn:test:rq2:GateB2", "occupied", True)
    g = load_graph_from_api("esc2")
    querys.check_hierarchy(g)
    
    
    # ============================================
    # Scenario 3 & 4: Associative relationships (Gate <-> Plane)
    # Demonstrates dynamic assignment and reasoning over associations
    # ============================================
    print("\nAssociative relationships (Gate <-> Plane)")
    print("------------------------------------------------------------")
    
    print("\nScenario 3: Assigning planes to all except one gates and verifying associative relationships...")
    print("Expected: NOT COLLAPSED")
    setPlane("urn:test:rq2:GateA1", "urn:test:rq2:Plane1")
    setPlane("urn:test:rq2:GateA2", "urn:test:rq2:Plane2")
    setPlane("urn:test:rq2:GateB1", "urn:test:rq2:Plane3")
    g = load_graph_from_api("esc3")
    querys.check_associative(g)
    
    # Add additional plane to test KG flexibility in updating relationships
    print("\nScenario 4: Assigning Plane4 to GateB2 and re-checking associative relations...")
    print("Expected: COLLAPSED")
    setPlane("urn:test:rq2:GateB2", "urn:test:rq2:Plane4")
    g = load_graph_from_api("esc4")
    querys.check_associative(g)
    
    
    # ============================================
    # Scenario 5 & 6: Peer-to-Peer (P2P) relationships between planes
    # Demonstrates KG reasoning over interactions among entities with dynamic properties
    # ============================================
    print("\nPeer-to-Peer (P2P) relationships between planes")
    print("------------------------------------------------------------")
    
    print("\nScenario 5: Updating flying status of planes so that two of them are flying and validating peer-to-peer relationships...")
    print("Expected: NOT COLLAPSED")
    set_property("urn:test:rq2:Plane1", "flying", False)
    set_property("urn:test:rq2:Plane2", "flying", False)
    set_property("urn:test:rq2:Plane3", "flying", False)
    set_property("urn:test:rq2:Plane4", "flying", True)
    set_property("urn:test:rq2:Plane5", "flying", True)
    g = load_graph_from_api("esc5")
    querys.check_p2p(g)
    
    print("\nScenario 6: Updating Plane5 flying status to False and re-checking peer-to-peer relationships...")
    print("Expected: COLLAPSED")
    set_property("urn:test:rq2:Plane5", "flying", False)
    g = load_graph_from_api("esc6")
    querys.check_p2p(g)


    # ============================================
    # Summary
    # These scenarios demonstrate that KGs enable expressive representation
    # and reasoning over relationships between CDT entities, including hierarchy,
    # associative, and peer-to-peer dependencies.
    # ============================================
    print("\nTest complete: KG reasoning over hierarchical, associative, and peer-to-peer relationships verified.")

if __name__ == "__main__":
    main()
