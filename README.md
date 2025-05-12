# Concert Ticket Booking Platform - Backend System

This project implements a backend system for a concert ticket booking platform using a microservices architecture with .NET 8, MongoDB, and Redis. It handles user authentication, concert management, real-time ticket inventory, and a high-concurrency booking process.

## Table of Contents

- [Project Overview](#project-overview)
- [Features](#features)
- [Architecture](#architecture)
- [Technology Stack](#technology-stack)
- [Prerequisites](#prerequisites)
- [Setup and Installation](#setup-and-installation)
- [Running the Services](#running-the-services)
- [API Endpoints](#api-endpoints)
  - [AuthService (Port 5001)](#authservice-port-5001)
  - [ConcertService (Port 5002)](#concertservice-port-5002)
  - [BookingService (Port 5003)](#bookingservice-port-5003)
  - [Internal Endpoints](#internal-endpoints)
- [Data Seeding](#data-seeding)
- [Load Testing](#load-testing)
- [Future Enhancements](#future-enhancements)

---

## Project Overview

This backend system provides the core infrastructure for booking concert tickets online. Key functionalities include:

*   User registration and JWT-based authentication.
*   Management of concerts, venues, dates, and seat types.
*   Real-time tracking of ticket availability using Redis.
*   A robust booking system designed to handle concurrent requests safely, preventing overbooking and duplicate bookings per user/concert.
*   Automatic disabling of bookings for past events.

---

## Features

### Core Requirements

*   **User Management:** Secure user registration and login (`AuthService`).
*   **Concert Listing:** API to list all available concerts (`ConcertService`).
*   **Concert Details:** API to view specific concert details, including seat types (e.g., VIP, Regular) and *real-time* remaining tickets for each type (`ConcertService` retrieving counts from `BookingService`/Redis).
*   **Ticket Booking:** API allowing authenticated users to book one ticket per concert for a chosen seat type (`BookingService`).
*   **Inventory Control:** Prevents booking if tickets for the chosen seat type are sold out (`BookingService` using Redis).
*   **Concurrency Safety:**
    *   Prevents duplicate bookings (one booking per user per concert).
    *   Prevents overbooking via atomic Redis operations during high load.

### Bonus Features Implemented

*   **Cancel Booking:** API for users to cancel their confirmed bookings, updating inventory (`BookingService`).
*   **Simulated Email Confirmation:** Logs simulated email confirmations for bookings and cancellations (configurable via `MailKit` settings in `BookingService`).
*   **Automatic Booking Disablement:** A background service (`ConcertService`) disables bookings for concerts once their start time passes, updating concert status and clearing Redis inventory via `BookingService`.
*   **Microservices Architecture:** Decoupled services (`AuthService`, `ConcertService`, `BookingService`) for better scalability and maintainability.
*   **Load Testing Setup:** Basic k6 scripts and guidance provided.

---

## Architecture

### Microservices

1.  **AuthService:**
    *   Handles user registration (`/api/auth/register`) and login (`/api/auth/login`).
    *   Issues JWTs for authenticated users.
    *   Stores user data in MongoDB (`AuthDB`).
2.  **ConcertService:**
    *   Manages concert details (name, date, venue, description, seat types, prices, total capacity).
    *   Provides APIs for listing (`/api/concerts`) and viewing details (`/api/concerts/{id}`).
    *   Includes endpoints for adding concerts and seat types (e.g., `/api/concerts`).
    *   Stores concert data in MongoDB (`ConcertDB`).
    *   Communicates with `BookingService` (HTTP) to fetch real-time ticket counts and manage inventory lifecycle (initialization, disabling).
    *   Runs a background service (`ConcertStatusUpdaterService`) to disable past concerts.
3.  **BookingService:**
    *   Handles ticket booking (`/api/bookings`) and cancellation (`/api/bookings/{bookingId}`).
    *   Validates JWTs received from users.
    *   Uses **Redis** for atomic management of ticket inventory (counts per concert/seat type).
    *   Stores booking records in MongoDB (`BookingDB`).
    *   Communicates with `ConcertService` (HTTP) to fetch concert details (e.g., price, name, status).
    *   Handles simulated email confirmations via logging.

### Data Stores

*   **MongoDB:** Primary persistent storage for Users, Concerts, and Bookings across the services.
*   **Redis:** In-memory cache used by `BookingService` for fast, atomic operations on ticket inventory counts, crucial for preventing race conditions during booking.

---

## Technology Stack

*   **.NET 8:** Backend framework.
*   **ASP.NET Core Web API:** Building RESTful services.
*   **MongoDB & MongoDB.Driver:** NoSQL database and .NET driver.
*   **Redis & StackExchange.Redis:** In-memory data store and .NET client.
*   **JWT (System.IdentityModel.Tokens.Jwt):** Authentication tokens.
*   **Docker & Docker Compose:** Containerization and orchestration.
*   **BCrypt.Net-Next:** Password hashing (`AuthService`).
*   **MailKit (Optional):** Email sending library (`BookingService`).
*   **k6 (Optional):** Load testing tool.

---

## Prerequisites

*   **Docker Desktop:** Ensure Docker and Docker Compose are installed and running. [Download Docker](https://www.docker.com/products/docker-desktop/)
*   **API Client:** Postman, Insomnia, or similar for interacting with the APIs.
*   **.NET 8 SDK (Optional):** Required only for local development *outside* Docker. [Download .NET](https://dotnet.microsoft.com/download/dotnet/8.0)
*   **k6 (Optional):** For running load tests. [Install k6](https://k6.io/docs/getting-started/installation/)
*   **(Optional) Mailtrap.io Account:** For testing actual email sending (configure in `BookingService/appsettings.json`).

---

## Setup and Installation

1.  **Clone the Repository:**
    ```bash
    git clone <your-repository-url>
    cd ConcertBookingPlatform
    ```

2.  **Configure Environment (If Necessary):**
    Most settings in `appsettings.json` within each service are pre-configured for Docker Compose networking. Review/update if needed:

    *   **`src/AuthService/appsettings.json`**:
        *   `JwtSettings:Key`: Secure key for JWT signing. **Change the default for production.**
    *   **`src/BookingService/appsettings.json`**:
        *   `JwtSettings:Key`, `JwtSettings:Issuer`: Ensure these match `AuthService` settings.
        *   `EmailSettings`: Update with SMTP details (e.g., Mailtrap) to test actual email sending.
          ```json
          "EmailSettings": {
            "SmtpServer": "sandbox.smtp.mailtrap.io",
            "SmtpPort": 2525,
            "SmtpUser": "YOUR_MAILTRAP_USERNAME",
            "SmtpPass": "YOUR_MAILTRAP_PASSWORD",
            "FromAddress": "no-reply@concertbooking.com",
            "FromName": "Concert Booking Platform",
            "UseSsl": false
          }
          ```

3.  **Build Docker Images:**
    From the project root directory (`ConcertBookingPlatform`):
    ```bash
    docker-compose build
    ```
    *(Use `docker-compose build --no-cache` to force a rebuild if needed)*

---

## Running the Services

1.  **Start Services with Docker Compose:**
    ```bash
    docker-compose up -d
    ```
    *(This starts all services in detached mode)*

2.  **View Logs:**
    ```bash
    # View logs for all services (follow mode)
    docker-compose logs -f

    # View logs for a specific service (e.g., bookingservice)
    docker-compose logs -f bookingservice
    ```

3.  **Access Services:**
    *   AuthService: `http://localhost:5001`
    *   ConcertService: `http://localhost:5002`
    *   BookingService: `http://localhost:5003`
    *   MongoDB: `localhost:27017` (via DB tools)
    *   Redis: `localhost:6379` (via Redis clients)

4.  **Stop Services:**
    ```bash
    # Stop and remove containers (keeps named volumes like mongo_data)
    docker-compose down

    # Stop and remove containers AND volumes (deletes DB data)
    # docker-compose down -v
    ```

---

## API Endpoints

*(Providing a Postman Collection is highly recommended for easier testing)*

### AuthService (Port 5001)

*   **`POST /api/auth/register`**
    *   Description: Registers a new user.
    *   Request Body:
        ```json
        {
          "username": "testuser",
          "email": "test@example.com",
          "password": "password123"
        }
        ```
    *   Response: `200 OK` (with user details + JWT), `400 Bad Request` (validation fails/user exists).

*   **`POST /api/auth/login`**
    *   Description: Logs in an existing user.
    *   Request Body:
        ```json
        {
          "username": "testuser",
          "password": "password123"
        }
        ```
    *   Response: `200 OK` (with user details + JWT), `400 Bad Request` (invalid credentials).

### ConcertService (Port 5002)

*   **`GET /api/concerts`**
    *   Description: Lists all available concerts with details and real-time remaining seats per type.
    *   Response: `200 OK` (array of concerts).

*   **`GET /api/concerts/{id}`**
    *   Description: Gets details for a specific concert.
    *   Response: `200 OK` (concert object), `404 Not Found`.

*   **`POST /api/concerts`** (Admin-like, currently unprotected)
    *   Description: Creates a new concert.
    *   Request Body Example:
        ```json
        {
          "name": "The Grand Symphony",
          "venue": "Classical Hall",
          "date": "2026-08-15T00:00:00Z", // Use UTC
          "startTime": "2026-08-15T19:30:00Z", // Use UTC
          "description": "An evening of classical masterpieces.",
          "seatTypes": [
            { "name": "Orchestra", "price": 120.00, "totalSeats": 150 },
            { "name": "Balcony", "price": 80.00, "totalSeats": 300 }
          ]
        }
        ```
    *   Response: `201 Created` (with created concert object).

*   **`POST /api/concerts/{concertId}/seattypes`** (Admin-like, currently unprotected)
    *   Description: Adds a new seat type to an existing concert.
    *   Request Body Example:
        ```json
        {
          "name": "Mezzanine",
          "price": 95.00,
          "totalSeats": 100
        }
        ```
    *   Response: `201 Created` (with created seat type object).

### BookingService (Port 5003)

*Requires `Authorization: Bearer <token>` header for all endpoints.*

*   **`POST /api/bookings`**
    *   Description: Creates a new booking for the authenticated user.
    *   Request Body:
        ```json
        {
          "concertId": "actual_concert_id_from_concert_service",
          "seatTypeId": "actual_seat_type_id_from_concert_service"
        }
        ```
    *   Response: `201 Created` (booking details), `400 Bad Request`, `401 Unauthorized`, `404 Not Found`, `409 Conflict` (sold out, already booked, concert invalid/disabled).

*   **`DELETE /api/bookings/{bookingId}`**
    *   Description: Cancels a booking owned by the authenticated user.
    *   Response: `204 No Content`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `409 Conflict`.

*   **`GET /api/bookings/mybookings`**
    *   Description: Retrieves all bookings made by the authenticated user.
    *   Response: `200 OK` (array of booking objects).

*   **`GET /api/bookings/{id}`**
    *   Description: Retrieves a specific booking by ID, if owned by the authenticated user.
    *   Response: `200 OK` (booking object), `404 Not Found`, `403 Forbidden`.

### Internal Endpoints

*(Used for service-to-service communication)*

*   **BookingService:** `POST /api/internal/inventory/initialize`
    *   Called by `ConcertService` to set initial ticket counts in Redis.
    *   Body: `{ "concertId": "...", "seatTypes": [{ "seatTypeId": "...", "count": ... }] }`

*   **BookingService:** `POST /api/internal/inventory/disable-concert/{concertId}`
    *   Called by `ConcertService` background job to clear inventory for past concerts.

*   **BookingService:** `GET /api/inventory/concert/{concertId}/seattype/{seatTypeId}`
    *   Called by `ConcertService` to get real-time remaining ticket count.

---

## Data Seeding

*   **ConcertService:** On startup, if the `Concerts` collection in `ConcertDB` is empty, sample concert data (including future and past concerts) is automatically seeded.
*   **BookingService Interaction:** During seeding, `ConcertService` calls `BookingService`'s internal endpoint to initialize the corresponding ticket inventories in Redis.

---

## Load Testing

*   **Tool:** k6
*   **Script Location:** A sample script (e.g., `loadtests/loadtest.js`) should be created based on the plan document (`load_testing_plan_k6`).
*   **Setup:**
    1.  Install k6 ([k6.io](https://k6.io)).
    2.  Register a dedicated test user (e.g., `testuser_for_load`).
    3.  Update the k6 script with:
        *   A valid JWT for the test user.
        *   Actual `TARGET_CONCERT_ID` and `TARGET_SEAT_TYPE_ID` from seeded data.
*   **Run Test (Local):**
    ```bash
    cd <directory_with_loadtest.js>
    k6 run loadtest.js
    ```
*   **Run Test (Docker):**
    1.  Add a `k6` service definition to `docker-compose.yml` (refer to `load_testing_plan_k6`).
    2.  Ensure the script targets service names (e.g., `http://bookingservice:8080/api/bookings`).
    3.  Mount the script into the container (e.g., to `/scripts`).
    4.  Run: `docker-compose run --rm k6 run /scripts/loadtest.js`

---

## Future Enhancements

*   Implement role-based authorization (e.g., Admin role for `POST /api/concerts`).
*   Replace direct HTTP calls with a message queue (RabbitMQ/Kafka) for improved resilience (e.g., inventory updates).
*   Implement distributed transaction/saga patterns for critical operations like booking.
*   Add comprehensive unit and integration tests.
*   Expand load testing scripts with more scenarios.
*   Implement pagination for list endpoints (`GET /api/concerts`, `GET /api/bookings/mybookings`).
*   Integrate a payment gateway.
*   Improve observability (distributed tracing, metrics).