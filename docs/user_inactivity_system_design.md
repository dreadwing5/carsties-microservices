# FastAPI and React User Inactivity Plan

## Summary

Implement user inactivity detection with React activity tracking, FastAPI as the source of truth, and WebSockets for live availability updates.

Store `last_active_at` in the database and derive availability as:

```py
available = now - last_active_at <= 20 minutes
```

No frontend polling is required. The user's own inactive component is shown locally after 20 minutes of no activity, while backend availability is based on `last_active_at`.

## Backend API

Add a FastAPI endpoint:

```http
POST /api/me/activity
```

Behavior:

- Requires an authenticated user.
- Updates the current user's `last_active_at` to the current UTC time.
- If the user was previously considered unavailable, broadcasts a WebSocket event that they are now available.
- Returns:

```json
{
  "ok": true,
  "available": true
}
```

Add availability derivation on the backend:

```py
from datetime import datetime, timedelta, timezone

INACTIVE_AFTER = timedelta(minutes=20)

def is_available(last_active_at: datetime | None) -> bool:
    return bool(
        last_active_at
        and datetime.now(timezone.utc) - last_active_at <= INACTIVE_AFTER
    )
```

Add availability fields to user or status responses where needed:

```json
{
  "user_id": "123",
  "available": true,
  "last_active_at": "2026-06-03T10:00:00Z"
}
```

## WebSocket Updates

Add a WebSocket endpoint:

```http
WS /ws/availability
```

Broadcast message shape:

```json
{
  "type": "user_presence_changed",
  "user_id": "123",
  "available": true,
  "last_active_at": "2026-06-03T10:00:00Z"
}
```

For v1, do not add a scheduler. Users become unavailable by derived state when read from the backend. A precise "became unavailable exactly at 20 minutes" event can be added later with a scheduler or Redis TTL if needed.

## React Behavior

Add a React inactivity hook that listens to:

```txt
mousemove, mousedown, keydown, scroll, touchstart, click, visibilitychange
```

The hook should:

- Reset a local 20-minute timer on activity.
- Show the inactive component when the local timer expires.
- Hide the inactive component when activity resumes.
- Send `POST /api/me/activity` when activity resumes after inactivity.
- Throttle activity API calls to at most once per minute while active.

Use `BroadcastChannel` for multi-tab coordination:

- Any tab can report activity to other tabs.
- All tabs reset their local inactivity timer when any tab observes activity.
- The currently visible tab should prefer sending `POST /api/me/activity`.
- If no tab is visible, the next visible or active tab sends the update.

Open a WebSocket connection from React to `/ws/availability`:

- Update visible availability UI when `user_presence_changed` events arrive.
- Reconnect with backoff after disconnects.

## Backend Data Flow

On user activity:

- FastAPI updates `last_active_at`.
- FastAPI computes previous availability using the old timestamp.
- If the previous state was unavailable and the new state is available, it broadcasts `available: true`.

On user or status reads:

- FastAPI computes `available` dynamically from `last_active_at`.
- If `last_active_at` is older than 20 minutes, the response returns `available: false`.

On browser sleep, crash, network loss, or closed tab:

- No special cleanup is required.
- Backend automatically treats the user as unavailable once `last_active_at` is older than 20 minutes.

## Test Plan

Backend tests:

- `is_available` returns `true` for a recent `last_active_at`.
- `is_available` returns `false` when `last_active_at` is older than 20 minutes.
- `POST /api/me/activity` updates `last_active_at`.
- `POST /api/me/activity` broadcasts `available: true` only when transitioning from inactive to active.
- User or status responses derive `available: false` without needing a stored unavailable flag.

Frontend tests:

- Activity events reset the 20-minute timer.
- The inactive component appears after 20 minutes with no activity.
- Activity in one tab resets timers in other tabs through `BroadcastChannel`.
- Activity API calls are throttled.
- WebSocket messages update availability UI.
- WebSocket reconnects after disconnect.

## Assumptions

- Authentication already exists in FastAPI and can provide `current_user`.
- The user model or table can add, or already has, `last_active_at`.
- Approximate inactive state is acceptable; exact 20-minute inactive broadcast is not required for v1.
- Database-backed `last_active_at` is the source of truth.
- WebSockets are used for live availability updates, not polling.
