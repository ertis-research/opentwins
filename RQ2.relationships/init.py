import json
from pathlib import Path
import requests
from rdflib import Graph, Literal, RDF, Namespace
from rdflib.namespace import XSD
from dotenv import load_dotenv
import os

load_dotenv()

THINGS_ENDPOINT = os.getenv("OTV2_THINGS_URL")
TWINS_ENDPOINT = os.getenv("OTV2_TWINS_URL")

def send_put(url, headers=None, json=None):
    resp = requests.put(url, headers=headers, json=json)
    try:
        resp.raise_for_status()
        print("[INFO] Successfully sent:", resp.json())
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    return resp

def send_post(url, headers=None, json=None):
    resp = requests.post(url, headers=headers, json=json)
    try:
        resp.raise_for_status()
        print("[INFO] Successfully sent:", resp.json())
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    return resp


def add_thing_to_twin(thingId):
    send_put(f"{TWINS_ENDPOINT}/twins/urn:test:rq2/things/{thingId}")

def post_thing(json_filename, add_id=""):
    json_path = Path(json_filename)
    if not json_path.exists():
        raise FileNotFoundError(f"File not found: {json_filename}")

    with open(json_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    data["id"] = data["id"] + add_id
    data["title"] = data["title"] + add_id

    url = f"{THINGS_ENDPOINT}/things"
    headers = {"Content-Type": "application/json"}
    send_post(url, headers=headers, json=data)
    
    return data["id"]

def prepare_base():
    
    listId = []
    # Web of things
    listId.append(post_thing("thingDescriptions/gate.json", "A1"))
    listId.append(post_thing("thingDescriptions/gate.json", "A2"))
    listId.append(post_thing("thingDescriptions/gate.json", "B1"))
    listId.append(post_thing("thingDescriptions/gate.json", "B2"))
    listId.append(post_thing("thingDescriptions/terminalA.json"))
    listId.append(post_thing("thingDescriptions/terminalB.json"))
    listId.append(post_thing("thingDescriptions/airport.json"))
    for i in range(1, 6):
        listId.append(post_thing("thingDescriptions/plane.json", str(i)))
        
    # Create twin
    send_post(f"{TWINS_ENDPOINT}/twins/urn:test:rq2")
    add_thing_to_twin(",".join(listId))
    print(listId)



