---
sidebar_position: 3
---

# API Documentation

Welcome to the Example API documentation. This API allows developers to interact with our services easily and efficiently.

## Table of Contents

- [Introduction](#introduction)
- [Endpoints](#endpoints)
  - [GET /users](#get-users)
  - [POST /users](#post-users)

## Introduction

This API provides access to FMU data and allows for the creation and management of simulation schemas and simulations. It is based on REST and returns responses in JSON format.

## Endpoints

### FMU endpoints

| Method | Endpoint                     | Description                                 |
|--------|------------------------------|---------------------------------------------|
| GET    | /fmi/fmus/{context}          | Retrieve a list of FMUs                     |
| POST   | /fmi/fmus/{context}          | Upload a new FMU                            |
| GET    | /fmi/fmus/{context}/{fmuName}| Retrieve FMU information within a context   |
| DELETE | /fmi/fmus/{context}/{fmuName}| Delete a FMU within a context               |


---

### Schema endpoints

| Method | Endpoint                          | Description                                 |
|--------|-----------------------------------|---------------------------------------------|
| GET    | /fmi/schemas/{context}            | Retrieve a list of schemas                  |
| POST   | /fmi/schemas/{context}            | Upload a new schema                         |
| GET    | /fmi/schemas/{context}/{schema_id}| Retrieve a schema within a context          |
| DELETE | /fmi/schemas/{context}/{schema_id}| Delete a schema within a context            |

---

### Simulation endpoints

| Method | Endpoint                                         | Description                               |
|--------|--------------------------------------------------|-------------------------------------------|
| GET    | /fmi/simulations/{context}                       | Retrieve a list of running simulations    |
| POST   | /fmi/simulations/{context}                       | Deploy simulation                         |
| GET    | /fmi/simulations/{context}/{simulation_id}       | Retrieve simulation info within a context |
| DELETE | /fmi/simulations/{context}/{simulation_id}       | Delete a simulation within a context      |
| POST   | /fmi/simulations/{context}/{simulation_id}/resume| Resume a specific simulation              |
| POST   | /fmi/simulations/{context}/{simulation_id}/pause | Stops a specific simulation               |
