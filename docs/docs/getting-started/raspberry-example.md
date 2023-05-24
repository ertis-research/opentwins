---
sidebar_position: 1
---

# Raspberry example

## Requirements
The only requisites are:
- Collect `IP` address of Ditto.
- Collect `USER` and `PASSWORD`

## First step. Creating the twin
First of all, you need to understand how twins work:
A twin has two main components:
- **attributes**. It contains the basic information of the twin, such as the name, location, etc.
- **features**. It contains the variables of the twin. Imagine a twin of a sensor that measures humidity and temperature. You will have two features: humidity and temperature. 
Each feature must contain a field called `properties` that contains, as its name says, every property of the feature, for example, the value of the temperature and the time the value has been measured.


Once we know wich data will store our twin, it is time to create it.
To create a twin, we need to make HTTP requests, we recommend you to use Postman. We need to create a `PUT` request to the Ditto url with the next pattern and a specific payload.

```bash
PUT http://{DITTO_IP}:{PORT}/api/2/things/{nameOfThing}
```

The payload has the attributes and features of the twin mentioned above. As attributes we have the location, in this case "Spain".

As features we have temperature and humidity. In this case both features has the same properties, value and timestamp, but they dont have to fit.
```json
{
    "attributes": {
        "location": "Spain"
    },
    "features": {
        "temperature": {
            "properties": {
                "value": null,
                "timestamp": null
            }
        },
        "humidity": {
            "properties": {
                "value": null,
                "timestamp": null
            }
        }
    }
}
```

Once we have checked that all the data is correct, just click send. You should recieve a 200 code of a correct execution.

To check if the twin has been created properly, just send a `GET` request to the same url.

```bash
GET http://{DITTO_IP}:{PORT}/api/2/things/{nameOfThing}
```

You should be granted with the schema of the new twin.