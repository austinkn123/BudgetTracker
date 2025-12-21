# Budget Tracker - Database Setup Guide

This guide will help you set up the database for the Budget Tracker application and connect it to the frontend dashboard.

## Prerequisites

- **Microsoft SQL Server** (LocalDB, SQL Server Express, or full SQL Server)
- **.NET 9.0 SDK** installed
- **Node.js** (v18 or higher) for the React frontend

## Database Setup

### Option 1: Using LocalDB (Recommended for Development)

LocalDB is automatically installed with Visual Studio and is the easiest option for local development.

1. **Verify LocalDB is installed:**
   ```bash
   sqllocaldb info
   ```

2. **Create the database:**
   ```bash
   sqllocaldb create MSSQLLocalDB
   sqllocaldb start MSSQLLocalDB
   ```

3. **Run the setup script:**
   
   Open SQL Server Management Studio (SSMS) or use sqlcmd:
   ```bash
   sqlcmd -S "(localdb)\MSSQLLocalDB" -i DatabaseSetup.sql
   ```

   Or use SSMS:
   - Connect to `(localdb)\MSSQLLocalDB`
   - Create a new database named `BudgetTracker`
   - Open and execute the `DatabaseSetup.sql` script

### Option 2: Using SQL Server Express or Full SQL Server

1. **Update the connection string** in `BudgetTracker.Server/appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "BudgetTrackerConnection": "Server=YOUR_SERVER;Database=BudgetTracker;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```

2. **Create the database** and run the setup script as described above.

## Database Schema

The application uses three main tables:

### Users Table
- `Id` (int, Primary Key, Identity)
- `CognitoUserId` (nvarchar(255)) - For AWS Cognito authentication
- `Email` (nvarchar(255))
- `CreatedAt` (datetime2)

### Categories Table
- `Id` (int, Primary Key, Identity)
- `UserId` (int, Foreign Key to Users)
- `Name` (nvarchar(100))

### Expenses Table
- `Id` (int, Primary Key, Identity)
- `UserId` (int, Foreign Key to Users)
- `CategoryId` (int, Foreign Key to Categories)
- `Amount` (decimal(18,2))
- `Date` (datetime2)
- `Merchant` (nvarchar(255), nullable)
- `Notes` (nvarchar(1000), nullable)
- `CreatedAt` (datetime2)

## Running the Application

### 1. Start the Backend API

```bash
cd BudgetTracker.Server
dotnet run
```

The API will start at `https://localhost:5001` (or similar port shown in console).

### 2. Start the Frontend

In a new terminal:

```bash
cd budgettracker.client
npm install
npm run dev
```

The frontend will start at `http://localhost:5173` (or similar port shown in console).

### 3. Access the Dashboard

Open your browser and navigate to `http://localhost:5173`

You should see the Budget Tracker Dashboard with:
- ✅ **Green banner** indicating successful database connection
- **User information** from the database
- **Categories** list
- **Expenses** table with sample data

## API Endpoints

The application exposes the following REST API endpoints:

### Users
- `GET /api/users/{id}` - Get user by ID
- `GET /api/users/cognito/{cognitoId}` - Get user by Cognito ID
- `POST /api/users` - Create new user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user

### Categories
- `GET /api/categories/{id}` - Get category by ID
- `GET /api/categories/user/{userId}` - Get all categories for a user
- `POST /api/categories` - Create new category
- `PUT /api/categories/{id}` - Update category
- `DELETE /api/categories/{id}` - Delete category

### Expenses
- `GET /api/expenses/{id}` - Get expense by ID
- `GET /api/expenses/user/{userId}` - Get all expenses for a user
- `POST /api/expenses` - Create new expense
- `PUT /api/expenses/{id}` - Update expense
- `DELETE /api/expenses/{id}` - Delete expense

## How the Frontend Handler Works

The frontend uses a service layer architecture to communicate with the backend:

### 1. API Client (`budgettracker.client/src/lib/api.ts`)
- Axios-based HTTP client configured to call `/api` endpoints
- Handles request/response transformations

### 2. Service Layer (`budgettracker.client/src/services/api.service.ts`)
- Three service objects: `userService`, `categoryService`, `expenseService`
- Each service provides CRUD operations for its entity type
- Uses TypeScript types for type safety

### 3. Dashboard Component (`budgettracker.client/src/components/Dashboard.tsx`)
- Uses React Query (`@tanstack/react-query`) for data fetching and caching
- Three queries: one each for user, categories, and expenses
- Automatic loading states and error handling
- Displays live data from the database

### Data Flow
```
User → Dashboard Component → React Query → Service Layer → API Client → Backend API → Repository → Database
```

## Troubleshooting

### Issue: Red error banner saying "Unable to connect to the database"

**Possible causes:**
1. Database is not running
2. Connection string is incorrect
3. Backend API is not running
4. Database tables don't exist

**Solutions:**
1. Verify LocalDB is running: `sqllocaldb info`
2. Check connection string in `appsettings.Development.json`
3. Ensure backend is running: `cd BudgetTracker.Server && dotnet run`
4. Run the `DatabaseSetup.sql` script

### Issue: "No user data found for ID 1"

**Solution:** The dashboard is looking for a user with ID 1. Run the `DatabaseSetup.sql` script to create sample data.

### Issue: Backend returns 404 or 500 errors

**Solution:** 
1. Check that all database tables exist
2. Verify the repository is properly registered in `Program.cs`
3. Check backend console logs for detailed error messages

## Development Notes

- **Current User ID:** The application is hardcoded to use User ID = 1 for demonstration purposes
- **Authentication:** AWS Cognito integration is planned but not yet implemented
- **CORS:** Pre-configured for local development

## Next Steps

1. **Add authentication** using AWS Cognito
2. **Implement user registration** and login flows
3. **Add data visualization** with charts and graphs
4. **Implement filtering** and search functionality
5. **Add budget planning** features

## Technology Stack

### Backend
- .NET 9.0 with ASP.NET Core Minimal APIs
- Dapper for data access
- Microsoft SQL Server
- Clean Architecture (Core, Application, Infrastructure layers)

### Frontend
- React 19 with TypeScript
- TanStack Query (React Query) for server state management
- Axios for HTTP requests
- Tailwind CSS for styling
- date-fns for date formatting

## Contributing

When adding new features that interact with the database:

1. **Add domain model** to `BudgetTracker.Core/Models`
2. **Add repository interface** to `BudgetTracker.Application/Interfaces`
3. **Implement repository** in `BudgetTracker.Infrastructure/Repositories`
4. **Add endpoints** to `BudgetTracker.Server/Endpoints`
5. **Update frontend types** in `budgettracker.client/src/types/api.ts`
6. **Add service methods** in `budgettracker.client/src/services/api.service.ts`
7. **Use in components** with React Query hooks

## License

[Add your license here]
