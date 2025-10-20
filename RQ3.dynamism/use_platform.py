import json
from pathlib import Path
from rdflib import Graph
import requests
from dotenv import load_dotenv
import os

load_dotenv()

RDF_FORMAT = "nquads"

THINGS_ENDPOINT = os.getenv("OTV2_THINGS_URL")
TWINS_ENDPOINT = os.getenv("OTV2_TWINS_URL")


def send_put(url, headers=None, json=None):
    resp = requests.put(url, headers=headers, json=json)
    try:
        resp.raise_for_status()
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    return resp

def send_delete(url, headers=None):
    resp = requests.delete(url, headers=headers)
    try:
        resp.raise_for_status()
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    return resp

def send_post(url, headers=None, json=None):
    resp = requests.post(url, headers=headers, json=json)
    try:
        resp.raise_for_status()
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    return resp

def send_get(url, headers=None):
    resp = requests.get(url, headers=headers)
    try:
        resp.raise_for_status()
        return resp.json()
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        raise

def remove_and_init_DB():
    resp = requests.delete(TWINS_ENDPOINT + "/graphdb/all")
    try:
        resp.raise_for_status()
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    resp = requests.put(TWINS_ENDPOINT + "/graphdb/init")
    try:
        resp.raise_for_status()
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
    return resp

def delete_subscription(thingId, eventName):
    send_delete(f"{THINGS_ENDPOINT}/things/{thingId}/subscriptions/{eventName}")

def post_twin(twinId):
    send_post(f"{TWINS_ENDPOINT}/twins/{twinId}")

def add_thing_to_twin(twinId, thingIds):
    send_put(f"{TWINS_ENDPOINT}/twins/{twinId}/things/{thingIds}")

def get_thing_state(thingId):
    return send_get(f"{THINGS_ENDPOINT}/things/{thingId}/state")

def post_thing(json_filename, uid=None, name=None):
    json_path = Path(json_filename)
    if not json_path.exists():
        raise FileNotFoundError(f"File not found: {json_filename}")

    with open(json_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    if(uid != None): data["id"] = uid
    if(name != None): data["title"] = name

    url = f"{THINGS_ENDPOINT}/things"
    headers = {"Content-Type": "application/json"}
    send_post(url, headers=headers, json=data)
    
    return data["id"]
    
def set_property(thingId, property, value):
    data = {property: value}
    resp = requests.put(f"{THINGS_ENDPOINT}/things/{thingId}/state", json=data)
    try:
        resp.raise_for_status()
        #print("[INFO] Successfully sent")
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    return resp

def add_link(thingId, linkData):
    resp = requests.post(f"{THINGS_ENDPOINT}/things/{thingId}/links", json=linkData)
    try:
        resp.raise_for_status()
        #print("[INFO] Successfully sent")
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    return resp

def update_link(thingId, target, relName, linkData):
    resp = requests.put(f"{THINGS_ENDPOINT}/things/{thingId}/links/{relName}/{target}", json=linkData)
    try:
        resp.raise_for_status()
        #print("[INFO] Successfully sent")
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    return resp

def delete_link(thingId, targetId, relName):
    resp = requests.delete(f"{THINGS_ENDPOINT}/things/{thingId}/links/{relName}/{targetId}")
    try:
        resp.raise_for_status()
        #print("[INFO] Successfully sent")
    except requests.HTTPError as e:
        print("[ERROR] Request failed:", e, resp.text)
        
    return resp

def load_graph_from_api(twinId, name):
    headers = {"Accept": "application/n-quads"}
    resp = requests.get(f"{TWINS_ENDPOINT}/twins/{twinId}", headers=headers)
    resp.raise_for_status()

    g = Graph()
    g.parse(data=resp.text, format=RDF_FORMAT)
    os.makedirs("output", exist_ok=True)
    g.serialize(f"output/{name}.ttl", format="turtle")
    if len(g) == 0:
        print("[ERROR] Graph empty")
    return g

