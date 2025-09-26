#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
RQ2 - To what extent can KGs provide a more expressive means of representing and reasoning about the relationships among CDTs?
"""

import json
from pathlib import Path
import requests
from rdflib import Graph, Literal, RDF, Namespace
from rdflib.namespace import XSD
from dotenv import load_dotenv
import os
import init

load_dotenv()

EX = Namespace("http://example.org/airport#")
THINGS_ENDPOINT = os.getenv("OTV2_TWINS_URL")
TWINS_ENDPOINT = os.getenv("OTV2_TWINS_URL")
RDF_FORMAT = "nquads"


    

def load_graph_from_api():
    headers = {"Accept": "application/n-quads"}
    resp = requests.get(TWINS_ENDPOINT + "/twins/urn:test:rq2", headers=headers)
    resp.raise_for_status()

    g = Graph()
    g.parse(data=resp.text, format=RDF_FORMAT)
    g.serialize("graph.ttl", format="turtle")
    return g

def check_airport_collapse(g):
    query = f"""
    PREFIX ex: <{EX}>

    SELECT ?airport (COUNT(DISTINCT ?gate) AS ?totalGates) (COUNT(DISTINCT ?occGate) AS ?occupiedGates)
    WHERE {{
      ?airport a ex:Airport ;
               ex:hasChild ?terminal .
      ?terminal ex:hasChild ?gate .
      OPTIONAL {{
        ?plane ex:atGate ?occGate .
        FILTER(?occGate = ?gate)
      }}
    }}
    GROUP BY ?airport
    HAVING (COUNT(DISTINCT ?gate) = COUNT(DISTINCT ?occGate))
    """

    results = list(g.query(query))
    return results

def main():
    
    init.prepare_base()
    
    print("Loading graph from API…")
    g = load_graph_from_api()
    print(f"Graph loaded with {len(g)} triples")

    #results = check_airport_collapse(g)

    #if results:
    #    for row in results:
    #        print(f"[INFO] Airport {row.airport} is COLLAPSED "
    #            f"(total gates={row.totalGates}, occupied={row.occupiedGates})")
    #else:
    #    print("[INFO] Airport still has free gates")

if __name__ == "__main__":
    main()
