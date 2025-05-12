// loadtest.js
import http from "k6/http";
import { check, sleep, group } from "k6";
import { Rate, Counter } from "k6/metrics";

const BASE_URL_AUTH = "http://localhost:5001/api";
const BASE_URL_BOOKING = "http://localhost:5003/api";

const TARGET_CONCERT_ID = "681f61b6b422f2b79c9d4b98";
const TARGET_SEAT_TYPE_ID = "681f61b6b422f2b79c9d4b95";

const bookingErrorRate = new Rate("booking_errors");
const successfulBookings = new Counter("successful_bookings");
const expectedConflictResponses = new Counter("expected_conflict_responses");

export const options = {
  vus: 10,
  duration: "20s",

  thresholds: {
    http_req_failed: ["rate<0.01"], // Overall HTTP errors should be less than 1%
    http_req_duration: ["p(95)<1000"], // 95% of requests should be below 1000ms (adjust as needed)
    booking_errors: ["rate<0.01"], // Custom: Less than 1% unexpected booking errors
    successful_bookings: ["count>=0"], // At least 0 successful bookings (will depend on available tickets)
    expected_conflict_responses: ["count>=0"], // At least 0 expected conflicts
    "http_req_duration{group:::User Authentication}": ["p(95)<500"],
    "http_req_duration{group:::Ticket Booking}": ["p(95)<800"],
  },
};

const testUserCredentials = {
  username: "testuser_for_load", // Create this user if they don't exist
  password: "password123",
};

// --- Setup function (runs once before the test) ---
export function setup() {
  console.log("k6 setup phase starting...");

  if (
    TARGET_CONCERT_ID === "your_target_concert_id_here" ||
    TARGET_SEAT_TYPE_ID === "your_target_seat_type_id_here"
  ) {
    console.error(
      "ERROR: Please replace placeholder TARGET_CONCERT_ID and TARGET_SEAT_TYPE_ID in the script."
    );
  }
  console.log(
    `Targeting Concert ID: ${TARGET_CONCERT_ID}, Seat Type ID: ${TARGET_SEAT_TYPE_ID}`
  );
  console.log("Setup phase complete. Test environment should be ready.");
  return { message: "Setup complete" };
}

// --- Virtual User (VU) Code (runs in a loop for each VU) ---
export default function (dataFromSetup) {
  let authToken = null;

  group("User Authentication", function () {
    const loginPayload = JSON.stringify(testUserCredentials);
    const loginParams = {
      headers: { "Content-Type": "application/json" },
      tags: { name: "AuthLogin" },
    };
    const loginRes = http.post(
      `${BASE_URL_AUTH}/auth/login`,
      loginPayload,
      loginParams
    );

    check(loginRes, {
      "[Auth] Login successful (200)": (r) => r.status === 200,
      "[Auth] Received auth token": (r) =>
        r.status === 200 &&
        r.json("token") !== null &&
        r.json("token") !== undefined,
    });

    if (loginRes.status === 200 && loginRes.json("token")) {
      authToken = loginRes.json("token");
    } else {
      console.error(
        `VU ${__VU} Login failed: Status ${loginRes.status} - Body: ${loginRes.body}`
      );
      return;
    }
    sleep(Math.random() * 2 + 0.5);
  });

  if (!authToken) {
    console.warn(`VU ${__VU} has no auth token, skipping booking attempt.`);
    return;
  }

  group("Ticket Booking", function () {
    const bookingPayload = JSON.stringify({
      concertId: TARGET_CONCERT_ID,
      seatTypeId: TARGET_SEAT_TYPE_ID,
    });

    const bookingParams = {
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${authToken}`,
      },
      tags: { name: "BookingAttempt" },
    };

    const bookingRes = http.post(
      `${BASE_URL_BOOKING}/bookings`,
      bookingPayload,
      bookingParams
    );

    const isBookingSuccessful = bookingRes.status === 201;
    const isExpectedConflict = bookingRes.status === 409;

    check(bookingRes, {
      "[Booking] Created (201) or Conflict (409)": (r) =>
        r.status === 201 || r.status === 409,
    });

    if (isBookingSuccessful) {
      successfulBookings.add(1);
      console.log(
        `VU ${__VU} successfully booked ticket for Concert: ${TARGET_CONCERT_ID}, Seat: ${TARGET_SEAT_TYPE_ID}. Booking ID: ${bookingRes.json(
          "id"
        )}`
      );
    } else if (isExpectedConflict) {
      expectedConflictResponses.add(1);
      console.log(
        `VU ${__VU} booking conflict (e.g. sold out/already booked by user) for Concert: ${TARGET_CONCERT_ID}, Seat: ${TARGET_SEAT_TYPE_ID}. Response: ${bookingRes.body}`
      );
    } else {
      console.error(
        `VU ${__VU} UNEXPECTED Booking Error: Status ${bookingRes.status} - Body: ${bookingRes.body} - For Concert: ${TARGET_CONCERT_ID}, Seat: ${TARGET_SEAT_TYPE_ID}`
      );
      bookingErrorRate.add(1);
    }
    sleep(Math.random() * 3 + 1);
  });
}

export function teardown(data) {
  console.log("k6 teardown phase starting...");
  console.log(
    "Teardown phase complete. Check logs, Redis, and MongoDB for final state."
  );
  console.log(`Test data from setup: ${data.message}`);
}
