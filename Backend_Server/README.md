# Unity Game Server

Backend server for Unity game integration with MongoDB (sessions) and MySQL (user data & points).

## Project Structure

```
.
├── app.js                 # Main entry point - connects everything
├── db/
│   ├── mongo/
│   │   └── connectMongoDB.js    # MongoDB connection (sessions)
│   └── mysql/
│       ├── connectMySQL.js       # MySQL connection (user data)
│       └── schema.sql            # Database schema
├── routes/
│   ├── authRoutes.js     # Authentication routes (login, register, logout)
│   └── gameRoutes.js     # Game routes (points, game data)
├── middleware/
│   └── auth.js           # Authentication middleware
└── package.json
```

## Setup

1. Install dependencies:
```bash
npm install
```

2. Create a `.env` file with the following variables:
```
PORT=3000
NODE_ENV=development

# MongoDB (for sessions)
MONGO_URL=your-mongodb-connection-string
MONGO_DB_NAME=game-session-db
SESSION_SECRET=your-session-secret
SESSION_CRYPTO_SECRET=your-crypto-secret

# MySQL (DigitalOcean - for user data & points)
MYSQL_HOST=your-mysql-host
MYSQL_USER=your-mysql-user
MYSQL_PASSWORD=your-mysql-password
MYSQL_DATABASE=unity_game_db
MYSQL_SSL=true
```

3. Set up MySQL database:
   - Run `db/mysql/schema.sql` on your DigitalOcean MySQL database

4. Start the server:
```bash
npm start
```

## API Endpoints

### Authentication
- `POST /auth/register` - Register a new user
- `POST /auth/login` - Login user
- `POST /auth/logout` - Logout user
- `GET /auth/me` - Get current user info

### Game
- `GET /game/points` - Get user points
- `PUT /game/points` - Update user points
- `POST /game/points/add` - Add points to user
- `GET /game/start` - Start game (requires auth)

### Unity Compatibility
- `GET /` - Server status
- `POST /` - Login endpoint (matches Unity GameLogic.cs)

## Unity Integration

The server is compatible with your Unity `GameLogic.cs` file:
- GET request to `/` returns server status
- POST request to `/` with form data (`id` and `password`) handles login

Make sure your Unity code uses:
```csharp
form.AddField("id", id_input.text);
form.AddField("password", password_input.text);
```

## Database

- **MongoDB**: Handles session storage
- **MySQL**: Stores user information and points

