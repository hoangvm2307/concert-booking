# Concert Ticket Booking Platform - Backend System

This project implements a backend system for a concert ticket booking platform using a microservices architecture with .NET 8, MongoDB, and Redis. It supports user registration, concert listing, detailed concert views (including seat types and availability), and a high-concurrency booking system designed to prevent overbooking and duplicate bookings.

## Table of Contents

- [Project Overview](#project-overview)
- [Features](#features)
  - [Core Requirements](#core-requirements)
  - [Bonus Features Implemented](#bonus-features-implemented)
- [Architecture](#architecture)
  - [Microservices](#microservices)
  - [Data Stores](#data-stores)
- [Technology Stack](#technology-stack)
- [Prerequisites](#prerequisites)
- [Setup and Installation](#setup-and-installation)
  - [1. Clone the Repository](#1-clone-the-repository)
  - [2. Configure Environment Variables (if necessary)](#2-configure-environment-variables-if-necessary)
  - [3. Build and Run with Docker Compose](#3-build-and-run-with-docker-compose)
- [API Endpoints & Usage](#api-endpoints--usage)
  - [AuthService (Port 5001)](#authservice-port-5001)
  - [ConcertService (Port 5002)](#concertservice-port-5002)
  - [BookingService (Port 5003)](#bookingservice-port-5003)
  - [Internal Endpoints](#internal-endpoints)
- [Data Seeding](#data-seeding)
- [Load Testing (with k6)](#load-testing-with-k6)
- [Future Enhancements / To-Do](#future-enhancements--to-do)

## Project Overview

This system provides the backend infrastructure for booking concert tickets. It handles user authentication, concert and seat type management, real-time ticket inventory, and the booking process itself, with a strong emphasis on handling concurrent requests safely.

## Features

### Core Requirements

* **User Management:** Users can register and log in (JWT-based authentication).
* **Concert Listing:** API to list all available concerts.
* **Concert Details:** API to view concert details, including:
    * All seat types (e.g., VIP, Regular, Standing).
    * Real-time remaining tickets for each seat type (via Redis, updated by BookingService).
* **Booking API:**
    * Users can book one ticket per concert.
    * Users must choose a seat type.
    * Bookings are prevented if tickets for the chosen seat type are sold out.
* **Concurrency & Data Integrity:**
    * Prevents duplicate bookings (one booking per user per concert).
    * Prevents overbooking, even with high concurrency (using Redis for atomic ticket count operations).

### Bonus Features Implemented

* **Cancel Booking API:** Users can cancel their confirmed bookings, freeing up seats (inventory updated in Redis).
* **Simulated Email Confirmation:** Email confirmations for bookings and cancellations are simulated via logging (and can be configured to send actual emails via MailKit, e.g., using Mailtrap for testing).
* **Automatic Disabling of Bookings:** A background service in `ConcertService` automatically disables bookings for concerts once their start time has passed. This involves updating the concert status and clearing/disabling inventory in Redis via `BookingService`.
* **Microservices Architecture:** Implemented with three primary services:
    * `AuthService`
    * `ConcertService`
    * `BookingService`
* **Load Testing Setup:** Basic k6 load testing scripts and guidance provided to simulate concurrent users.

## Architecture

### Microservices

The system is composed of the following .NET 8 microservices:

1.  **AuthService:**
    * Handles user registration and login.
    * Issues JWT (JSON Web Tokens) for authenticated users.
    * Uses MongoDB to store user credentials.
2.  **ConcertService:**
    * Manages concert information (name, date, venue, description, seat types, prices, total seats).
    * Provides APIs to list concerts and view details.
    * Includes admin-like functions to add concerts and seat types.
    * Uses MongoDB to store concert data.
    * Communicates with `BookingService` to initialize/update ticket inventory in Redis and to fetch real-time remaining ticket counts.
    * Includes a background service to automatically disable bookings for past concerts.
3.  **BookingService:**
    * Handles the ticket booking process.
    * Validates JWTs from `AuthService`.
    * Uses Redis for atomic management of ticket inventory (decrementing counts, checking availability).
    * Uses MongoDB to store booking records.
    * Communicates with `ConcertService` to fetch concert details (e.g., price, name, booking eligibility).
    * Provides an API for users to cancel their bookings.
    * Handles simulated email confirmations.

### Data Stores

* **MongoDB:** Primary persistent data store for:
    * Users (`AuthDB` in `AuthService`)
    * Concerts and their seat types (`ConcertDB` in `ConcertService`)
    * Bookings (`BookingDB` in `BookingService`)
* **Redis:** In-memory data store used for:
    * Atomically managing ticket counts for each concert seat type to handle high concurrency and prevent overbooking.
    * Accessed primarily by `BookingService`.

## Technology Stack

* **.NET 8:** Framework for building the microservices.
* **ASP.NET Core Web API:** For creating RESTful APIs.
* **MongoDB:** NoSQL document database.
* **MongoDB.Driver:** .NET driver for MongoDB.
* **Redis:** In-memory key-value store.
* **StackExchange.Redis:** .NET client for Redis.
* **MailKit:** For sending emails (used for simulated email confirmations).
* **JWT (JSON Web Tokens):** For stateless authentication.
* **Docker & Docker Compose:** For containerization and orchestration of services.
* **k6 (Optional):** For load testing.
* **BCrypt.Net-Next:** For password hashing in `AuthService`.

## Prerequisites

* **Docker Desktop:** Ensure Docker and Docker Compose are installed and running on your system. (Download from [docker.com](https://www.docker.com/products/docker-desktop/))
* **.NET 8 SDK (Optional, for local development outside Docker):** (Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0))
* **k6 (Optional, for running load tests):** (Installation instructions at [k6.io](https://k6.io/docs/getting-started/installation/))
* **API Client (e.g., Postman, Insomnia):** For interacting with the APIs.
* **(Optional for Email Testing) Mailtrap.io Account:** If you want to test the email sending feature with Mailtrap, sign up for a free account and get your SMTP credentials.

## Setup and Installation

### 1. Clone the Repository

```bash
git clone <your-repository-url>
cd ConcertBookingPlatform

2. Configure Environment Variables (if necessary)
Most configurations are handled via appsettings.json within each service and are set up to work with the Docker Compose service names. However, review the following:

src/AuthService/appsettings.json:

JwtSettings:Key: A strong secret key for JWT signing. A default is provided. For production, this should be managed securely and not hardcoded.

src/BookingService/appsettings.json:

JwtSettings:Key & JwtSettings:Issuer: If you configured BookingService for symmetric key JWT validation (Option 1 in our discussion), ensure these match AuthService.

EmailSettings: Update with your SMTP provider details (e.g., Mailtrap credentials) if you want to test actual email sending.

"EmailSettings": {
  "SmtpServer": "sandbox.smtp.mailtrap.io",
  "SmtpPort": 2525, // Or your Mailtrap port
  "SmtpUser": "YOUR_MAILTRAP_USERNAME",
  "SmtpPass": "YOUR_MAILTRAP_PASSWORD",
  "FromAddress": "no-reply@concertbooking.com",
  "FromName": "Concert Booking Platform",
  "UseSsl": false // Adjust based on your Mailtrap port (e.g., true for 465)
}

3. Build and Run with Docker Compose
From the root directory of the project (ConcertBookingPlatform):

# Build the Docker images for all services (use --no-cache if you want to force a full rebuild)
docker-compose build

# Start all services in detached mode (in the background)
docker-compose up -d

# To view logs for all services:
docker-compose logs -f

# To view logs for a specific service (e.g., bookingservice):
docker-compose logs -f bookingservice

# To stop and remove all containers, networks, and volumes (use with caution if you want to keep data):
# docker-compose down -v

# To stop and remove containers but keep named volumes (mongo_data, redis_data):
docker-compose down

The services will be available at the following default host ports:

AuthService: http://localhost:5001

ConcertService: http://localhost:5002

BookingService: http://localhost:5003

MongoDB: localhost:27017 (accessible by database tools)

Redis: localhost:6379 (accessible by Redis clients)

API Endpoints & Usage
(It's highly recommended to provide a Postman Collection for easier testing.)

AuthService (Port 5001)
POST /api/auth/register

Registers a new user.

Request Body:

{
  "username": "testuser",
  "email": "test@example.com",
  "password": "password123"
}

Response: 200 OK with user details and JWT token, or 400 Bad Request if validation fails or user exists.

POST /api/auth/login

Logs in an existing user.

Request Body:

{
  "username": "testuser",
  "password": "password123"
}

Response: 200 OK with user details and JWT token, or 400 Bad Request for invalid credentials.

ConcertService (Port 5002)
GET /api/concerts

Lists all available concerts with their details (including seat types and real-time remaining seats).

Response: 200 OK with an array of concert objects.

GET /api/concerts/{id}

Gets details for a specific concert by its ID.

Response: 200 OK with concert object, or 404 Not Found.

POST /api/concerts (Admin-like endpoint, currently not protected by roles)

Creates a new concert.

Request Body Example:

{
  "name": "The Grand Symphony",
  "venue": "Classical Hall",
  "date": "2026-08-15T00:00:00Z",
  "startTime": "2026-08-15T19:30:00Z",
  "description": "An evening of classical masterpieces.",
  "seatTypes": [
    { "name": "Orchestra", "price": 120.00, "totalSeats": 150 },
    { "name": "Balcony", "price": 80.00, "totalSeats": 300 }
  ]
}

Response: 201 Created with the created concert object.

POST /api/concerts/{concertId}/seattypes (Admin-like endpoint)

Adds a new seat type to an existing concert.

Request Body Example:

{
  "name": "Mezzanine",
  "price": 95.00,
  "totalSeats": 100
}

Response: 201 Created with the created seat type object.

BookingService (Port 5003)
All endpoints require a valid JWT in the Authorization: Bearer <token> header.

POST /api/bookings

Creates a new booking for the authenticated user.

Request Body:

{
  "concertId": "actual_concert_id_from_concert_service",
  "seatTypeId": "actual_seat_type_id_from_concert_service"
}

Response: 201 Created with booking details, or 400 Bad Request, 401 Unauthorized, 404 Not Found, 409 Conflict (sold out, already booked, concert started/disabled).

DELETE /api/bookings/{bookingId}

Cancels a booking for the authenticated user.

bookingId is the ID of the booking to cancel.

No request body.

Response: 204 No Content on success, or error codes like 401, 403, 404, 409.

GET /api/bookings/mybookings

Retrieves all bookings made by the authenticated user.

Response: 200 OK with an array of booking objects.

GET /api/bookings/{id}

Retrieves a specific booking by its ID, if owned by the authenticated user.

Response: 200 OK with booking object, or 404 Not Found.

Internal Endpoints
BookingService: POST /api/internal/inventory/initialize

Called by ConcertService (e.g., during seeding or concert creation/update) to initialize ticket counts in Redis.

Request Body:

{
  "concertId": "string",
  "seatTypes": [
    { "seatTypeId": "string", "count": 0 }
  ]
}

BookingService: POST /api/internal/inventory/disable-concert/{concertId}

Called by ConcertService's background job when a concert starts to clear its inventory from Redis.

BookingService: GET /api/inventory/concert/{concertId}/seattype/{seatTypeId}

Called by ConcertService to get the real-time remaining ticket count for a specific seat type.

Data Seeding
ConcertService: On startup, if the Concerts collection in ConcertDB is empty, sample concert data (including future and one past concert) will be seeded automatically. This process also triggers calls to BookingService to initialize the corresponding ticket inventories in Redis.

Load Testing (with k6)
A sample k6 script (loadtests/loadtest.js if you create this structure) is provided in the project documentation (see load_testing_plan_k6 Canvas/Document ID).

Install k6: Follow instructions at k6.io.

Update Script: Modify loadtest.js with actual TARGET_CONCERT_ID and TARGET_SEAT_TYPE_ID from your seeded data. Ensure the test user (testuser_for_load) is registered.

Run Test:

cd <directory_containing_loadtest.js>
k6 run loadtest.js

To run k6 within Docker (alternative):

Update docker-compose.yml to include the k6 service (see load_testing_plan_k6 Canvas/Document ID for the service definition).

Ensure your k6 script targets service names (e.g., http://authservice:8080) instead of localhost.

Run: docker-compose run --rm k6 run /scripts/loadtest.js (assuming script is mounted to /scripts).

Future Enhancements / To-Do
Implement proper role-based authorization for admin endpoints.

Use a message queue (e.g., RabbitMQ, Kafka) for more resilient inter-service communication instead of direct HTTP calls (e.g., for inventory updates).

Implement a more robust distributed transaction/saga pattern for booking if database save fails after Redis decrement.

Add comprehensive unit and integration tests for all services.

Enhance load testing scripts for more varied scenarios and user behaviors.

Implement pagination for list endpoints (e.g., GET /api/concerts).

Consider adding a payment gateway integration.

Ref