# mqtt.py
import json
import time
import logging
from dotenv import load_dotenv
import paho.mqtt.client as mqtt
import os

# Load environment variables from .env file
load_dotenv()

# Configure global logger
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S"
)
logger = logging.getLogger(__name__)


class MQTTClient:
    """
    A simple MQTT client wrapper for publishing JSON messages to a given topic.
    """

    def __init__(self):
        """
        Initialize the MQTT client and connect to the broker.

        Args:
            broker_host (str): Hostname or IP address of the MQTT broker.
            broker_port (int): Port number of the MQTT broker (default 1883).
            client_id (str): Unique ID for this MQTT client.
        """
        self.broker_host = os.getenv("MQTT_HOST", "localhost")
        self.broker_port = int(os.getenv("MQTT_PORT", 1883))

        logger.info(f"Initializing MQTT client for broker {self.broker_host}:{self.broker_port}")
        self.client = mqtt.Client()

        try:
            self.client.connect(self.broker_host, self.broker_port, keepalive=60)
            logger.info("Successfully connected to MQTT broker.")
        except Exception as e:
            logger.error(f"Failed to connect to MQTT broker: {e}")
            raise e

    def send_message(self, topic: str, msg: dict) -> float:
        """
        Publishes a JSON message to the specified MQTT topic and returns the publish timestamp.

        Args:
            topic (str): The MQTT topic to publish to.
            msg (dict): The message payload as a Python dictionary.

        Returns:
            float: The UNIX timestamp of successful message publication.
        """
        try:
            payload = json.dumps(msg)
            logger.debug(f"Serialized payload: {payload}")

            logger.info(f"Publishing message to topic '{topic}'...")
            result = self.client.publish(topic, payload)

            # Wait for the message to be published
            result.wait_for_publish()

            if result.is_published():
                timestamp = time.time()
                logger.info(f"Message successfully published to '{topic}' at {timestamp}.")
                return timestamp
            else:
                logger.error(f"Failed to publish message to topic '{topic}'.")
                raise Exception(f"Publish operation did not complete for topic '{topic}'")

        except Exception as e:
            logger.exception(f"Error while sending MQTT message: {e}")
            raise e