---
sidebar_position: 3
---

# API Documentation

Welcome to the Example API documentation. This API allows developers to interact with our services easily and efficiently.

## Table of Contents

- [Introduction](#introduction)
- [Endpoints](#endpoints)
  - [FMU endpoints](#fmu-endpoints)
  - [Schema endpoints](#schema-endpoints)
  - [Simulation endpoints](#simulation-endpoints)

## Introduction

This API provides access to FMU data and allows for the creation and management of simulation schemas and simulations. It is based on REST and returns responses in JSON format.

## Endpoints

### FMU endpoints

| Method | Endpoint /fmi/fmus  | Description                                 |
|--------|---------------------|---------------------------------------------|
| [GET](#1-get-context)    | /{context}          | Retrieve a list of FMUs                     |
| [POST](#2-post-context)   | /{context}          | Upload a new FMU                            |
| [GET](#3-get-contextfmuname)    | /{context}/{fmuName}| Retrieve FMU information within a context   |
| [DELETE](#4-delete-contextfmuname) | /{context}/{fmuName}| Delete a FMU within a context               |


#### 1. GET /{context}
##### Description
Get a list of FMUs in a specific context.

##### Request
**Method:** ``GET``  
**URL:** `/{context}`

##### Response
**Status code:** `200 OK`  
**Response Body:** A list of FMUs with information about their variables.
```json
[
    {
        "id": "bouncingBallCS2",
        "inputs": [],
        "outputs": [],
        "other_variables": [
            {
                "name": "h",
                "type": "Real",
                "default": {
                    "start": "1"
                },
                "description": "height, used as state"
            },
            {
                "name": "v",
                "type": "Real",
                "default": {
                    "start": "0",
                    "reinit": "true"
                },
                "description": "velocity of ball, used as state"
            },
            {
                "name": "g",
                "type": "Real",
                "default": {
                    "start": "9.81"
                },
                "description": "acceleration of gravity"
            },
            {
                "name": "e",
                "type": "Real",
                "default": {
                    "start": "0.7",
                    "min": "0.5",
                    "max": "1"
                },
                "description": "dimensionless parameter"
            }
        ]
    }
]
```

#### 2. POST /{context}
##### Description
Upload a new FMU to the sistem into a specific context.

##### Request
**Method:** ``POST``  
**URL:** `/{context}`

##### Response
**Status code:** `200 OK`  
**Response Body:**
```json
"FMU uploaded succesfully"
```

#### 3. GET /{context}/{fmuName}
##### Description
Get the XML file of a specific FMU.

##### Request
**Method:** ``GET``  
**URL:** `/{context}/{fmuName}`

##### Response
**Status code:** `200 OK`  
**Response Body:**
```xml
<?xml version="1.0" encoding="ISO-8859-1"?>
<fmiModelDescription
  fmiVersion="2.0"
  modelName="bouncingBall"
  guid="{8c4e810f-3df3-4a00-8276-176fa3c9f003}"
  numberOfEventIndicators="1">
    <CoSimulation
  modelIdentifier="bouncingBall"
  canHandleVariableCommunicationStepSize="true"/>
    <LogCategories>
        <Category name="logAll"/>
        <Category name="logError"/>
        <Category name="logFmiCall"/>
        <Category name="logEvent"/>
    </LogCategories>
    <ModelVariables>
        <ScalarVariable name="h" valueReference="0" description="height, used as state"
                  causality="local" variability="continuous" initial="exact">
            <Real start="1"/>
        </ScalarVariable>
        <ScalarVariable name="der(h)" valueReference="1" description="velocity of ball"
                  causality="local" variability="continuous" initial="calculated">
            <Real derivative="1"/>
        </ScalarVariable>
        <ScalarVariable name="v" valueReference="2" description="velocity of ball, used as state"
                  causality="local" variability="continuous" initial="exact">
            <Real start="0" reinit="true"/>
        </ScalarVariable>
        <ScalarVariable name="der(v)" valueReference="3" description="acceleration of ball"
                  causality="local" variability="continuous" initial="calculated">
            <Real derivative="3"/>
        </ScalarVariable>
        <ScalarVariable name="g" valueReference="4" description="acceleration of gravity"
                  causality="parameter" variability="fixed" initial="exact">
            <Real start="9.81"/>
        </ScalarVariable>
        <ScalarVariable name="e" valueReference="5" description="dimensionless parameter"
                  causality="parameter" variability="tunable" initial="exact">
            <Real start="0.7" min="0.5" max="1"/>
        </ScalarVariable>
    </ModelVariables>
    <ModelStructure>
        <Derivatives>
            <Unknown index="2" />
            <Unknown index="4" />
        </Derivatives>
        <InitialUnknowns>
            <Unknown index="2"/>
            <Unknown index="4"/>
        </InitialUnknowns>
    </ModelStructure>
</fmiModelDescription>
```

#### 4. DELETE /{context}/{fmuName}
##### Description
Delete a FMU from the sistem in a specific context.

##### Request
**Method:** ``DELETE``  
**URL:** `/{context}/{fmuName}`

##### Response
**Status code:** `200 OK`  
**Response Body:**
```json
"FMU deleted succesfully"
```



---

### Schema endpoints

| Method | Endpoint /fmi/schemas             | Description                                 |
|--------|-----------------------------------|---------------------------------------------|
| [GET](#1-get-context-1)    | /{context}            | Retrieve a list of schemas                  |
| [POST](#2-post-context-1)   | /{context}            | Upload a new schema                         |
| [GET](#3-get-contextschema_id)    | /{context}/{schema_id}| Retrieve a schema within a context          |
| [DELETE](#4-delete-contextschema_id) | /{context}/{schema_id}| Delete a schema within a context            |

#### 1. GET /{context}
##### Description
Get all simulation schemas in a specific context.

##### Request
**Method:** ``GET``  
**URL:** `/{context}`

##### Response
**Status code:** `200 OK`  
**Response Body:** A list of name and id of every single simulation schemas.
```json
[
  {
      "id": "schema1",
      "name": "Schema 1"
  }
]
```

#### 2. POST /{context}
##### Description
Create a new schema in a specific context.

##### Request
**Method:** ``POST``  
**URL:** `/{context}`

##### Response
**Status code:** `200 OK`  
**Response Body:** A list of name and id of every single simulation schemas.
```json
"Schema created succesfully"
```


#### 3. GET /{context}/{schema_id}
##### Description
Get a schema in a specific context.

##### Request
**Method:** ``GET``  
**URL:** `/{context}/{schema_id}`

##### Response
**Status code:** `200 OK`  
**Response Body:**
```json
[
    {
        "id": "schema2",
        "fmus": [
            {
                "id": "Controller",
                "inputs": [
                    {
                        "id": "u_s"
                    },
                    {
                        "id": "u_m"
                    }
                ],
                "outputs": [
                    {
                        "id": "y"
                    }
                ]
            },
            {
                "id": "Drivetrain",
                "inputs": [
                    {
                        "id": "tau"
                    }
                ],
                "outputs": [
                    {
                        "id": "w"
                    }
                ]
            }
        ],
        "name": "Schema 2",
        "schema": [
            {
                "to": {
                    "id": "Controller",
                    "var": "u_s"
                },
                "from": {
                    "var": "w_ref"
                }
            },
            {
                "to": {
                    "id": "Controller",
                    "var": "u_m"
                },
                "from": {
                    "id": "Drivetrain",
                    "var": "w"
                }
            },
            {
                "to": {
                    "id": "Drivetrain",
                    "var": "tau"
                },
                "from": {
                    "id": "Controller",
                    "var": "y"
                }
            },
            {
                "to": {
                    "var": "w"
                },
                "from": {
                    "id": "Drivetrain",
                    "var": "w"
                }
            }
        ],
        "description": "Testing schema",
        "relatedTwins": [
            "Twin1"
        ]
    }
]
```


#### 4. DELETE /{context}/{schema_id}
##### Description
Delete a schema in a specific context.

##### Request
**Method:** ``DELETE``  
**URL:** `/{context}/{schema_id}`

##### Response
**Status code:** `200 OK`  
**Response Body:**
```json
"Schema deleted succesfully"
```



---

### Simulation endpoints

| Method                                    | Endpoint   /fmi/simulations      | Description                               |
|-------------------------------------------|----------------------------------|-------------------------------------------|
| [GET](#1-get-context-2)                     | /{context}                       | Retrieve a list of running simulations    |
| [POST](#2-post-context-2)                   | /{context}                       | Deploy simulation                         |
| [GET](#3-get-contextsimulation_id)        | /{context}/{simulation_id}       | Retrieve simulation info within a context |
| [DELETE](#4-delete-contextsimulation_id)  | /{context}/{simulation_id}       | Delete a simulation within a context      |
| [POST](#5-post-contextsimulation_idresume)| /{context}/{simulation_id}/resume| Resume a specific simulation              |
| [POST](#6-post-contextsimulation_idpause) | /{context}/{simulation_id}/pause | Stops a specific simulation               |

#### 1. GET /{context}
##### Description
Get all running simulations in a specific context.

##### Request
**Method:** ``GET``  
**URL:** `/{context}`

##### Response
**Status code:** `200 OK`  
**Response Body:** A list of information of every single running simulations.
```json

[
    {
        "schema-id": "schema1",
        "simulation-id": "pruebabouncingball",
        "namespace": "opentwins",
        "type": "one-time",
        "status": "Active",
        "pods": [
            {
                "simulation-id": "pruebabouncingball",
                "phase": "Running",
                "status": false,
                "creation_timestamp": "2024/09/26, 03:06:06+0000"
            }
        ]
    }
]

```


#### 2. POST /{context}
##### Description
Create a new simulation using a existing schema in a specific context.

##### Request
**Method:** ``POST``  
**URL:** `/{context}`

##### Response
**Status code:** `200 OK`  
**Response Body:**
```json
"true"
```

#### 3. GET /{context}/{simulation_id}
##### Description
Get all information about a specific simulation in a specific context.

##### Request
**Method:** ``GET``  
**URL:** `/{context}/{simulation_id}`

##### Response
**Status code:** `200 OK`  
**Response Body:**
```json
{
  "api_version": "batch/v1",
  "kind": "Job",
  "metadata": {
      "annotations": null,
      "creation_timestamp": "2024-09-26 03:06:06+00:00",
      "deletion_grace_period_seconds": null,
      "deletion_timestamp": null,
      .
      .
      .
  }
}
```

#### 4. DELETE /{context}/{simulation_id}
##### Description
Delete specific simulation in a specific context.

##### Request
**Method:** ``DELETE``  
**URL:** `/{context}/{simulation_id}`

##### Response
**Status code:** `200 OK`  
**Response Body:**
```json
"Schema deleted succesfully"
```

#### 5. POST /{context}/{simulation_id}/resume
##### Description
Resume paused simulation in a specific context.

##### Request
**Method:** ``POST``  
**URL:** `/{context}/{simulation_id}/resume`

##### Response
**Status code:** `200 OK`  

#### 6. POST /{context}/{simulation_id}/pause
##### Description
Pause simulation in a specific context.

##### Request
**Method:** ``POST``  
**URL:** `/{context}/{simulation_id}/pause`

##### Response
**Status code:** `200 OK`  