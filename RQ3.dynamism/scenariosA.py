import logging
import random
import time
from datetime import datetime, timezone
from dotenv import load_dotenv
from dateutil.parser import isoparse
import pandas as pd

import use_platform


# ========================================
# Configuration & Logging
# ========================================
load_dotenv()

logger = logging.getLogger(__name__)

# Thing URNs
PLANE_ID = "urn:test:rq3:Plane"
AIRPORT_ID = "urn:test:rq3:Airport"
TERMINAL_A_ID = "urn:test:rq3:TerminalA"
TERMINAL_B_ID = "urn:test:rq3:TerminalB"


# ========================================
# Utility: Send MQTT and validate
# ========================================
def send_msg_and_validate(mqtt_client, topic: str, msg: dict, thing_id: str, expected_result: dict):
    """
    Sends an MQTT message, retrieves the thing state, compares with expected, and measures latency.
    """
    timestamp = mqtt_client.send_message(topic, msg)
    sent_time = datetime.fromtimestamp(timestamp, tz=timezone.utc)
    logger.info(f"Message sent to topic '{topic}': {msg} at {sent_time.isoformat()}")

    time.sleep(3)  # allow propagation

    state = use_platform.get_thing_state(thing_id)
    state_values = {k: v["value"] for k, v in state.items()}

    match = state_values == expected_result

    try:
        latest_update = max(v["lastUpdate"] for v in state.values())
        latency = (isoparse(latest_update) - sent_time).total_seconds()
    except Exception as e:
        logger.error(f"Latency calculation error: {e}")
        latency = -1.0

    logger.info(f"Expected: {expected_result} | Actual: {state_values} | Match={match} | Latency={latency:.3f}s")
    return match, latency, state_values


# ========================================
# Scenario Definitions
# ========================================
# ========================================
# Scenario Definitions
# ========================================
def scenario_a1_a2_combined(mqtt_client, kafka_client):
    """
    Combined Scenario A1 + A2:
    A1: Verify reactive update works correctly.
    A2: Remove subscription and verify no further update occurs.
    """
    results = []

    # --- A1 ---
    use_platform.create_thing("thingDescriptions/plane.json", PLANE_ID, "Plane")
    msg1 = {
        "lightsOn": random.choice([True, False]),
        "altitude": random.randint(0, 12000),
        "status": random.choice(["Taxiing", "Climbing", "Cruising", "Landing"]),
    }

    match_a1, latency_a1, last_state = send_msg_and_validate(
        mqtt_client, "telemetry/plane_rq3", msg1, PLANE_ID, msg1
    )

    results.append({
        "scenario": "A1",
        "iteration": 1,
        "match": match_a1,
        "latency": latency_a1,
        "timestamp": datetime.now(timezone.utc).isoformat(),
    })

    # --- A2 ---
    send_request_time = datetime.now(timezone.utc)
    logging.info(f"Sending request to delete subscription for plane ID {PLANE_ID} at {send_request_time.isoformat()}")
    use_platform.delete_subscription(PLANE_ID, "mqtt:changes_plane_rq3")
    
    result = kafka_client.get_time_from_source(topic="thing.description.changes", source=PLANE_ID, start_timestamp=send_request_time.timestamp()*1000)
    latency_a2 = float("nan")
    if result != None:
        latency_a2 = (result - send_request_time).total_seconds()
        logging.info(f"Received change for plane ID {PLANE_ID}. Latency: {latency_a2:.3f} seconds")
    
    msg2 = {
        "lightsOn": random.choice([True, False]),
        "altitude": random.randint(0, 12000),
        "status": random.choice(["Taxiing", "Climbing", "Cruising", "Landing"]),
    }

    match_a2, _, _ = send_msg_and_validate(
        mqtt_client, "telemetry/plane_rq3", msg2, PLANE_ID, last_state
    )

    results.append({
        "scenario": "A2",
        "iteration": 1,
        "match": match_a2,
        "latency": latency_a2,
        "timestamp": datetime.now(timezone.utc).isoformat(),
    })

    return pd.DataFrame(results)


def scenario_a3_derived_property_propagation(mqtt_client):
    """Scenario A3: Verify that derived properties in a CDT (Airport) update correctly, measuring latency."""
    use_platform.create_thing("thingDescriptions/airport.json")
    use_platform.create_thing("thingDescriptions/terminalA.json")
    use_platform.create_thing("thingDescriptions/terminalB.json")

    # Generate random messages
    tA_msg = {"flights": random.randint(10, 100)}
    tB_msg = {"flights": random.randint(10, 100)}

    # Send messages and record timestamps
    ts_a = mqtt_client.send_message("telemetry/terminalA", tA_msg)
    sent_time_a = datetime.fromtimestamp(ts_a, tz=timezone.utc)
    ts_b = mqtt_client.send_message("telemetry/terminalB", tB_msg)
    sent_time_b = datetime.fromtimestamp(ts_b, tz=timezone.utc)

    time.sleep(2)

    # Retrieve airport state
    state = use_platform.get_thing_state(AIRPORT_ID)
    total_expected = tA_msg["flights"] + tB_msg["flights"]
    total_actual = state["total_flights"]["value"]

    match = total_expected == total_actual

    # Calculate latency based on lastUpdate of total_flights
    try:
        last_update_str = state["total_flights"]["lastUpdate"]
        last_update_time = isoparse(last_update_str)
        # Take max of sent_time_a and sent_time_b for reference
        sent_time = max(sent_time_a, sent_time_b)
        latency = (last_update_time - sent_time).total_seconds()
    except Exception as e:
        logger.error(f"Error calculating latency for A3: {e}")
        latency = float("nan")

    logger.info(f"Expected total_flights={total_expected} | Actual={total_actual} | Match={match} | Latency={latency:.3f}s")

    return pd.DataFrame([{
        "scenario": "A3",
        "iteration": 1,
        "match": match,
        "latency": latency,
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }])
