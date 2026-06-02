# Docker Concepts

## Volumes

In Docker, **volumes** are the preferred mechanism for saving data so it is not lost when a container shuts down, restarts, or is recreated.

By default, containers are ephemeral. If a database container is removed, any data stored only inside that container disappears too.

A volume solves this by storing data on the Docker host, outside the temporary container filesystem.

## Example: MongoDB in `docker-compose.yml`

The local compose file defines a named MongoDB volume like this:

```yaml
services:
  mongodb:
    image: mongo
    ports:
      - 27017:27017
    volumes:
      - mongodata:/data/db

volumes:
  mongodata:
```

Here is how this works:

1. **`mongodata`:** The named volume managed by Docker.
2. **`/data/db`:** The path inside the MongoDB container where MongoDB stores database files.

The mapping `mongodata:/data/db` tells Docker to persist anything MongoDB writes to `/data/db` in the `mongodata` volume.

Because of this, the MongoDB container can be destroyed and recreated without losing the database contents.

## Resetting Local Data

To remove the containers while keeping volumes:

```bash
docker compose down
```

To remove containers and delete the local database volumes:

```bash
docker compose down -v
```

Use `-v` carefully. It deletes the local Postgres and MongoDB data managed by the compose file.
