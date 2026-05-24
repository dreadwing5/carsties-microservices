# Docker Concepts

## Volumes

In Docker, **volumes** are the preferred mechanism for saving data so that it isn't lost when a container shuts down or restarts.

By default, Docker containers are "ephemeral" (temporary). If a container deletes itself or crashes, all the files and data inside it disappear forever. For applications like databases, this would result in a complete loss of all users, items, and settings.

A **Volume** solves this by storing the data safely on the host machine's hard drive, outside of the temporary container.

### Example: MongoDB in `docker-compose.yml`

In our `docker-compose.yml`, we define volumes for our databases like this:

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
1. **`mongodata`**: This is the named volume. It acts as a safe, persistent storage folder on the actual host machine (Docker completely manages where this lives internally).
2. **`/data/db`**: This is the internal folder *inside* the MongoDB container where MongoDB naturally tries to save all its database files.

By linking them together with the `:` symbol (`mongodata:/data/db`), we are telling Docker: *"Whenever MongoDB tries to write data into `/data/db`, actually save it into the permanent `mongodata` volume instead."* 

Because of this, the MongoDB container can be completely destroyed and recreated without any loss of data.
