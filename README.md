# Carsties Microservices Tutorial

## Introduction

Carsties is a microservices tutorial project designed to help developers learn and implement microservices architecture in .NET

This course covers building a complete auction application with multiple microservices, demonstrating real-world scenarios and industry-standard patterns for microservices development.

## What You'll Learn

This tutorial covers the following key concepts and technologies:

- **Microservices Architecture**: Building distributed systems with multiple independent services
- **Service Communication**: Implementing inter-service communication using RabbitMQ and gRPC
- **Identity Management**: Using IdentityServer as a centralized identity provider
- **API Gateway**: Creating a gateway using Microsoft YARP for request routing and load balancing
- **Modern Frontend**: Building a client-side application with Next.js using the App Router (Next.js 13.4+)
- **Real-time Communication**: Implementing push notifications using SignalR
- **Containerization**: Dockerizing all services for consistent deployment
- **CI/CD**: Setting up automated workflows using GitHub Actions
- **Orchestration**: Deploying and managing services with Docker Compose and Kubernetes
- **Testing**: Unit and integration testing strategies for microservices

## Prerequisites

Before you begin, ensure you have met the following requirements:

- **.NET 8.0 SDK**: [Download .NET](https://dotnet.microsoft.com/download)
- **Node.js (v18 or later)**: [Download Node.js](https://nodejs.org/)
- **Git**: [Download Git](https://git-scm.com/downloads)
- **Visual Studio Code**: [Download VS Code](https://code.visualstudio.com/) (recommended)
- **Docker Desktop**: [Download Docker](https://www.docker.com/products/docker-desktop/) (required for containerization)
- **Postman**: [Download Postman](https://www.postman.com/downloads/) for API testing
- **pnpm**: Install pnpm 🚀

  ```bash
  npm install -g pnpm
  ```

## Services Overview

### Backend Services (.NET 8)

- **Identity Service**: Handles authentication, authorization, and user management using IdentityServer
- **Auction Service**: Manages auction creation, updates, and core auction logic
- **Bid Service**: Processes bids, validates bid amounts, and manages bidding history
- **Notification Service**: Sends real-time notifications using SignalR
- **Search Service**: Provides search and filtering capabilities for auctions
- **Gateway Service**: API Gateway using Microsoft YARP for routing and load balancing

### Frontend (Next.js 14)

- **Modern React**: Built with Next.js App Router and React 18
- **TypeScript**: Full type safety throughout the application
- **Tailwind CSS**: Utility-first CSS framework for styling
- **Real-time Updates**: SignalR integration for live notifications
- **Responsive Design**: Mobile-first approach with modern UI/UX

## Technologies and Libraries

### Backend Stack

- **.NET 8.0**: Latest .NET framework for building microservices
- **Entity Framework Core**: ORM for database interactions
- **SQL Server**: Primary database (with SQLite for development)
- **IdentityServer**: Identity and access control
- **RabbitMQ**: Message broker for asynchronous communication
- **gRPC**: High-performance RPC framework for service-to-service communication
- **SignalR**: Real-time web functionality
- **YARP**: Yet Another Reverse Proxy for API Gateway
- **Serilog**: Structured logging framework
- **AutoMapper**: Object-object mapping
- **FluentValidation**: Input validation
- **MediatR**: Mediator pattern implementation

### Frontend Stack

- **Next.js 14**: React framework with App Router
- **TypeScript**: Typed JavaScript for better development experience
- **Tailwind CSS**: Utility-first CSS framework
- **React Query**: Data fetching and caching
- **Zustand**: Lightweight state management
- **Axios**: HTTP client for API communication
- **React Hook Form**: Form handling and validation
- **Zod**: Schema validation
- **React Hot Toast**: Notification system

### DevOps & Infrastructure

- **Docker**: Containerization platform
- **Docker Compose**: Multi-container application orchestration
- **Kubernetes**: Container orchestration (optional)
- **GitHub Actions**: CI/CD workflows
- **NGINX**: Reverse proxy and load balancer

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/dreadwing5/carsties-microservices.git
cd carsties-microservices
```

### 2. Start the Services

#### Option A: Using Docker Compose (Recommended)

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down
```

#### Option B: Manual Setup

1. **Start the Backend Services**:

   ```bash
   # Start each service individually
   cd services/identity-service
   dotnet run

   cd ../auction-service
   dotnet run

   # ... repeat for other services
   ```

2. **Start the Frontend**:

   ```bash
   cd client-app
   pnpm install
   pnpm dev
   ```

### 3. Access the Application

- **Frontend**: <http://localhost:3000>
- **API Gateway**: <http://localhost:5000>
- **Identity Service**: <http://localhost:5001>
- **Auction Service**: <http://localhost:5002>
- **Bid Service**: <http://localhost:5003>
- **Notification Service**: <http://localhost:5004>
- **Search Service**: <http://localhost:5005>

## Course Structure

This tutorial is designed to be hands-on and practical, with 90%+ of the content involving coding along with the instructor. The course is structured as follows:

### Main Course Content

1. **Project Setup and Architecture**
2. **Identity Service Development**
3. **Auction Service Implementation**
4. **Bid Service Creation**
5. **Notification Service with SignalR**
6. **Search Service Development**
7. **API Gateway Configuration**
8. **Frontend Development with Next.js**
9. **Service Communication Patterns**
10. **Docker Containerization**
11. **CI/CD with GitHub Actions**
12. **Local Deployment with Docker Compose**

### Optional Appendices

- **Unit and Integration Testing**
- **Local Kubernetes Deployment**
- **Cloud Kubernetes Deployment**

## Learning Resources

### Course Materials

- **Service Specifications**: Detailed specs for each microservice in `course-assets/specs/`
- **Code Snippets**: Useful code examples in `course-assets/snippets/`
- **API Collections**: Postman collections for testing in `course-assets/postman/`
- **Architecture Documentation**: System design and architecture guides

### External Resources

- [Udemy Course](https://www.udemy.com/course/build-a-microservices-app-with-dotnet-and-nextjs-from-scratch/) - Complete video tutorial
- [.NET Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Next.js Documentation](https://nextjs.org/docs)
- [Docker Documentation](https://docs.docker.com/)

## Development Workflow

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific service tests
cd services/auction-service
dotnet test
```

### Database Migrations

```bash
# Add migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update
```

### Code Quality

```bash
# Format code
dotnet format

# Run linting
pnpm lint

# Type checking
pnpm type-check
```

## Contributing

Contributions are welcome! Please follow these steps:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines

- Follow the existing code style and patterns
- Write tests for new functionality
- Update documentation as needed
- Ensure all services can be run with Docker Compose

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Based on the comprehensive microservices course by Neil Cummings
- Built with modern .NET and Next.js best practices
- Inspired by real-world microservices patterns and architectures

---

**Happy Learning! 🚀**

Start your microservices journey today and build scalable, maintainable applications with modern technologies.
