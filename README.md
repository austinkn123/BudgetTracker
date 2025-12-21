# BudgetTracker

A full-stack budget tracking application built with React, TypeScript, .NET Core, and SQL Server.

## 📋 Documentation

### Product Requirements
- **[AWS Cognito Authentication - Executive Summary](./docs/AWS-COGNITO-EXECUTIVE-SUMMARY.md)** - Business overview and high-level feature summary for AWS Cognito authentication with Gmail sign-in
- **[AWS Cognito Authentication - Requirements](./docs/REQUIREMENTS-AWS-COGNITO-AUTH.md)** - Detailed user stories, acceptance criteria, and technical specifications
- **[AWS Cognito Authentication - Implementation Guide](./docs/IMPLEMENTATION-GUIDE-AWS-COGNITO.md)** - Step-by-step technical guide for developers

### Technical Documentation
- **[Developer Instructions](./.instructions.md)** - Senior developer role definition and tech stack details

## 🚀 Upcoming Features

### Authentication & Authorization (In Planning)
The BudgetTracker application is planned to include AWS Cognito authentication with the following features:
- Sign in with Google/Gmail (OAuth 2.0)
- Optional email/password authentication
- Secure user session management
- Protected API endpoints with JWT validation
- User profile management
- Password reset flows

See the [AWS Cognito Executive Summary](./AWS-COGNITO-EXECUTIVE-SUMMARY.md) for full details.

## 🏗️ Current Architecture

### Backend (.NET Core)
- ASP.NET Core Web API with Minimal APIs
- Dapper for data access
- SQL Server database
- Layered architecture:
  - `BudgetTracker.Core` - Domain models
  - `BudgetTracker.Application` - Business logic
  - `BudgetTracker.Infrastructure` - Data access
  - `BudgetTracker.Server` - API endpoints

### Frontend (React + TypeScript)
- React 19 with TypeScript
- Vite for build tooling
- Material UI (MUI) for components
- TanStack Query for data fetching
- Tailwind CSS for styling
- React Router for navigation

### Database
- Microsoft SQL Server
- Dapper for query execution
- Data models: User, Category, Expense

## 📦 Getting Started

### Prerequisites
- Node.js 18+
- .NET 9.0 SDK
- SQL Server

### Installation

1. Clone the repository
2. Set up the database (connection string in appsettings.json)
3. Install backend dependencies:
   ```bash
   dotnet restore
   ```
4. Install frontend dependencies:
   ```bash
   cd budgettracker.client
   npm install
   ```
5. Run the application:
   ```bash
   dotnet run
   ```

The application will be available at `https://localhost:5173`

## 🔐 Planned Authentication

The application architecture already includes support for AWS Cognito authentication:
- User model includes `CognitoUserId` field
- Authorization middleware is configured but not yet enforced
- Ready for OAuth integration

Implementation of the authentication system is documented in the requirements and implementation guides.

## 📝 License

This project is part of a personal portfolio and learning exercise.