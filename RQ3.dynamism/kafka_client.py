from datetime import datetime
import logging
import os
import json
from kafka import KafkaConsumer, TopicPartition
from kafka.errors import NoBrokersAvailable
from dateutil import tz

logger = logging.getLogger(__name__)

class KafkaClient:
    def __init__(self, bootstrap_servers=None):
        """
        Initializes the Kafka client using the environment variable KAFKA_BOOTSTRAP_SERVERS
        if no bootstrap_servers parameter is provided.
        """
        self.bootstrap_servers = bootstrap_servers or os.getenv('KAFKA_BOOTSTRAP_SERVERS', 'localhost:9092')
        self.group_id = "hola"

    def get_time_from_source(self, topic, source, start_timestamp, timeout_ms=50000):
        """
        Reads messages from a topic starting from a given timestamp until one with the given 'source' field is found.
        Returns the value of the 'time' field from that message.

        Parameters:
            topic (str): Kafka topic name.
            source (str): Expected value of the 'source' field.
            start_timestamp (int): Unix timestamp in milliseconds from which to start reading.
            timeout_ms (int): Maximum waiting time in milliseconds.

        Returns:
            str | None: The 'time' value if a matching message is found, otherwise None.
        """
        consumer = None
        try:
            consumer = KafkaConsumer(
                bootstrap_servers=self.bootstrap_servers,
                enable_auto_commit=False,
                value_deserializer=lambda x: json.loads(x.decode('utf-8')),
                consumer_timeout_ms=timeout_ms,
                group_id=self.group_id
            )

            # Obtener las particiones del tópico
            partitions = consumer.partitions_for_topic(topic)
            if not partitions:
                logger.warning(f"No partitions found for topic {topic}")
                return None

            topic_partitions = [TopicPartition(topic, p) for p in partitions]

            # Asignar manualmente las particiones
            consumer.assign(topic_partitions)

            # Calcular offsets a partir del timestamp
            timestamps = {tp: start_timestamp for tp in topic_partitions}
            offsets = consumer.offsets_for_times(timestamps)
            
            for tp in topic_partitions:
                offset_data = offsets.get(tp)
                if offset_data and offset_data.offset is not None:
                    consumer.seek(tp, offset_data.offset)
                else:
                    # No hay mensajes posteriores al timestamp → no leer
                    logger.debug(f"No offsets after {start_timestamp} ms for {tp}")
                    return None

            logger.debug(f"Starting to consume from timestamp {start_timestamp} for topic {topic}")

            # Consumir mensajes hasta encontrar el 'source' deseado
            for message in consumer:
                value = message.value
                msg_source = value.get("source")
                msg_time = value.get("time")
                
                logger.debug(
                    f"KafkaTS={datetime.utcfromtimestamp(message.timestamp/1000)} | "
                    f"PayloadTime={value.get('time')}"
                )

                logger.debug(f"Message received: source={msg_source}, time={msg_time}")

                if msg_source == source:
                    logger.debug(f"Found matching source '{source}' with time={msg_time}")
                    return datetime.fromtimestamp(message.timestamp/1000, tz=tz.UTC)

            logger.warning("No matching message found before timeout.")
            return None

        except NoBrokersAvailable:
            logger.error(f"Failed to connect to Kafka brokers: {self.bootstrap_servers}. Check network and configuration.")
            return None
        except Exception as e:
            logger.error(f"An unexpected error occurred while consuming: {e}")
            return None
        finally:
            if consumer:
                consumer.close()
                logger.debug("Kafka Consumer closed.")
