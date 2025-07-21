using UnityEngine;
using Mono.Data.Sqlite;
using System.Data;
using System.IO;
using System.Collections.Generic;
using DBModels;
using Unity.VisualScripting;
using System;

public class DatabaseControl : MonoBehaviour
{
    private static string dbName = "MyGameDatabase.db";
    private static string dbPath => Path.Combine(Application.persistentDataPath, dbName);
    private static string connectionString => $"URI=file:{dbPath}";

    private static SqliteConnection _connection;
    [SerializeField]
    private bool clearDBOnStart = false;

    [SerializeField]
    private bool startLoad = false;

    public static bool isConnected = false;


    void Start()
    {
        
        InitializeConnection();
        if (clearDBOnStart)
        {
            ClearDatabase();
        }

        CreateDatabase();
        if (startLoad)
        {
            InsertGame("Battle Royale", "Survival", "easy", 0);
            InsertPlayer("Alice", 1);
            UpdateGameMode("Battle Royale", "Team Deathmatch");
        }
        
    }

    void OnDestroy()
    {
        CloseConnection();
    }

    public static void InitializeConnection()
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            isConnected = true;
            Debug.Log("Database connection opened.");
        }
    }

    public static void CloseConnection()
    {
        if (_connection != null)
        {
            _connection.Close();
            _connection.Dispose();
            _connection = null;
            Debug.Log("Database connection closed.");
        }
    }

    public static void CreateDatabase()
    {
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS games (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT,
                    mode TEXT,
                    difficulty TEXT,
                    starting_year INTEGER
                );

                CREATE TABLE IF NOT EXISTS nations (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT,
                    description TEXT,
                    game_id INTEGER,
                    FOREIGN KEY(game_id) REFERENCES games(id)
                );

                CREATE TABLE IF NOT EXISTS tiles (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    in_game_id INTEGER, -- in-game tile ID
                    game_id INTEGER,  -- unique game id
                    name TEXT,
                    description TEXT,
                    type TEXT,
                    population INTEGER,
                    infrastructure_rating INTEGER,
                    factories INTEGER,
                    stability INTEGER,
                    owner INTEGER,
                    FOREIGN KEY(owner) REFERENCES nations(id),
                    FOREIGN KEY(game_id) REFERENCES games(id)
                );

                CREATE TABLE IF NOT EXISTS tile_neighbors (
                    tile_id INTEGER,          -- tile.game_id
                    neighbor_id INTEGER,      -- neighbor.game_id
                    game_id INTEGER, -- game reference.
                    FOREIGN KEY(tile_id) REFERENCES tiles(game_id),
                    FOREIGN KEY(neighbor_id) REFERENCES tiles(game_id),
                    FOREIGN KEY(game_id) REFERENCES games(id)
                );
                CREATE TABLE IF NOT EXISTS players (
                    player_id  INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT,
                    game_id INTEGER,
                    FOREIGN KEY(game_id) REFERENCES games(id)
                );

                CREATE INDEX IF NOT EXISTS idx_tiles_game_id ON tiles(game_id);
                CREATE INDEX IF NOT EXISTS idx_tile_neighbors_tile_id ON tile_neighbors(tile_id);
                CREATE INDEX IF NOT EXISTS idx_tile_neighbors_neighbor_id ON tile_neighbors(neighbor_id);
                CREATE INDEX IF NOT EXISTS idx_players_id ON players(player_id);
            ";

            command.ExecuteNonQuery();
        }

        Debug.Log($"SQLite DB ready at: {dbPath}");
    }

    public static void InsertGame(string name, string mode, string difficulty, int startingYear)
    {
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
            INSERT INTO games (name, mode, difficulty, starting_year)
            VALUES (@name, @mode, @difficulty, @starting_year)";

            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@mode", mode);
            command.Parameters.AddWithValue("@difficulty", difficulty);
            command.Parameters.AddWithValue("@starting_year", startingYear);

            command.ExecuteNonQuery();
        }

        
        Debug.Log($"Game inserted: {name} [{mode}, {difficulty}, Year: {startingYear}]");
    }


    public static void InsertPlayer(string name, int gameId)
    {
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO players (name, game_id) VALUES (@name, @game_id)";
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@game_id", gameId);
            command.ExecuteNonQuery();
        }

        Debug.Log($"Player inserted: {name} → Game ID {gameId}");
    }

    public static void UpdateGameMode(string gameName, string newMode)
    {
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "UPDATE games SET mode = @mode WHERE name = @name";
            command.Parameters.AddWithValue("@mode", newMode);
            command.Parameters.AddWithValue("@name", gameName);
            command.ExecuteNonQuery();
        }

        Debug.Log($"Game updated: {gameName} → New Mode: {newMode}");
    }

    public static void ClearDatabase()
    {
        string[] clearCommands = new string[]
        {
        "DELETE FROM tile_neighbors;",
        "DELETE FROM tiles;",
        "DELETE FROM players;",
        "DELETE FROM games;",
        "DELETE FROM sqlite_sequence;"
        };

        foreach (string cmdText in clearCommands)
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = cmdText;
                command.ExecuteNonQuery();
            }
        }

        Debug.Log("All database data cleared.");
    }


    public static void InsertTile(DBTile tile)
    {
        // Insert tile itself
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
            INSERT INTO tiles (in_game_id, game_id, name, description, type, population, infrastructure_rating, factories, stability, owner)
            VALUES (@in_game_id, @game_id, @name, @description, @type, @population, @infra, @factories, @stability, @owner)";

            command.Parameters.AddWithValue("@in_game_id", tile.dbId);
            command.Parameters.AddWithValue("@game_id", tile.gameId);
            command.Parameters.AddWithValue("@name", tile.name);
            command.Parameters.AddWithValue("@description", tile.description);
            command.Parameters.AddWithValue("@type", tile.type);
            command.Parameters.AddWithValue("@population", tile.population);
            command.Parameters.AddWithValue("@infra", tile.infrastructureRating);
            command.Parameters.AddWithValue("@factories", tile.factories);
            command.Parameters.AddWithValue("@stability", tile.stability);
            command.Parameters.AddWithValue("@owner", tile.owner);
            command.ExecuteNonQuery();
            Debug.Log($"Inserting {tile.name} into tiles");
        }

        // Insert neighbor links
        foreach (int neighborGameId in tile.neighborGameIds)
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = @"
                INSERT INTO tile_neighbors (tile_id, neighbor_id, game_id)
                VALUES (@tile_game_id, @neighbor_game_id, @game_id)";
                command.Parameters.AddWithValue("@tile_game_id", tile.dbId);
                command.Parameters.AddWithValue("@neighbor_game_id", neighborGameId);
                command.Parameters.AddWithValue("@game_id", tile.gameId);
                command.ExecuteNonQuery();
            }
        }

        Debug.Log($"Tile inserted: {tile.name} (Game ID {tile.gameId}) with {tile.neighborGameIds.Count} neighbors.");
    }
    public static DBTile GetTileById(int id, int gameId)
    {
        DBTile tile = null;

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
            SELECT * FROM tiles WHERE in_game_id = @id AND game_id = @game_id";
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@game_id", gameId);

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    tile = new DBTile
                    {
                        dbId = reader.GetInt32(reader.GetOrdinal("in_game_id")),
                        gameId = reader.GetInt32(reader.GetOrdinal("game_id")),
                        name = reader.GetString(reader.GetOrdinal("name")),
                        description = reader.GetString(reader.GetOrdinal("description")),
                        type = reader.GetString(reader.GetOrdinal("type")),
                        population = Mathf.RoundToInt(reader.GetFloat(reader.GetOrdinal("population"))),
                        infrastructureRating = reader.GetInt32(reader.GetOrdinal("infrastructure_rating")),
                        factories = reader.GetInt32(reader.GetOrdinal("factories")),
                        stability = reader.GetInt32(reader.GetOrdinal("stability")),
                        owner = reader.GetInt32(reader.GetOrdinal("owner"))
                    };
                }
            }
        }

        if (tile != null)
        {
            tile.neighborGameIds = new List<int>();
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = @"
                SELECT neighbor_id FROM tile_neighbors 
                WHERE tile_id = @tile_id AND game_id = @game_id";
                command.Parameters.AddWithValue("@tile_id", id);
                command.Parameters.AddWithValue("@game_id", gameId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tile.neighborGameIds.Add(reader.GetInt32(0));
                    }
                }
            }
        }

        return tile;
    }

    public static DBTile[] GetAllTiles(int gameId)
    {
        List<DBTile> tiles = new List<DBTile>();

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
            SELECT in_game_id, game_id, name, description, type, population,
                   infrastructure_rating, factories, stability, owner
            FROM tiles
            WHERE game_id = @gameId";
            command.Parameters.AddWithValue("@gameId", gameId);

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    DBTile tile = new DBTile
                    {
                        dbId = reader.GetInt32(reader.GetOrdinal("in_game_id")),
                        gameId = reader.GetInt32(reader.GetOrdinal("game_id")),
                        name = reader.GetString(reader.GetOrdinal("name")),
                        description = reader.GetString(reader.GetOrdinal("description")),
                        type = reader.GetString(reader.GetOrdinal("type")),
                        population = Mathf.RoundToInt(reader.GetFloat(reader.GetOrdinal("population"))),
                        infrastructureRating = reader.GetInt32(reader.GetOrdinal("infrastructure_rating")),
                        factories = reader.GetInt32(reader.GetOrdinal("factories")),
                        stability = reader.GetInt32(reader.GetOrdinal("stability")),
                        owner = reader.GetInt32(reader.GetOrdinal("owner")),
                        neighborGameIds = new List<int>()
                    };

                    tiles.Add(tile);
                }
            }
        }

        // Load neighbors for each tile
        foreach (var tile in tiles)
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = @"
                SELECT neighbor_id FROM tile_neighbors
                WHERE tile_id = @tile_id AND game_id = @gameId";
                command.Parameters.AddWithValue("@tile_id", tile.dbId);
                command.Parameters.AddWithValue("@gameId", gameId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tile.neighborGameIds.Add(reader.GetInt32(0));
                    }
                }
            }
        }

        Debug.Log($"Loaded {tiles.Count} tiles for Game ID {gameId}.");
        return tiles.ToArray();
    }


    public static DB_Game[] GetListOfGames(int start =0, int end =10)
    {
        List<DB_Game> games = new List<DB_Game>();
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT id, name, mode, difficulty, starting_year
                FROM games
                ORDER BY id
                LIMIT @limit OFFSET @offset
            ";

            command.Parameters.AddWithValue("@limit", end - start);
            command.Parameters.AddWithValue("@offset", start);

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    DB_Game game = new DB_Game
                    {
                        id = reader.GetInt32(reader.GetOrdinal("id")),
                        name = reader.GetString(reader.GetOrdinal("name")),
                        mode = reader.GetString(reader.GetOrdinal("mode")),
                        difficulty = reader.GetString(reader.GetOrdinal("difficulty")),
                        startingYear = reader.GetInt32(reader.GetOrdinal("starting_year"))
                    };
                    games.Add(game);
                }
            }

        }

        Debug.Log($"Loaded {games.Count} games from index {start} to {end}.");
        return games.ToArray();
    }
    public static DB_Game GetGameById(int id)
    {
        DB_Game game = null;

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
            SELECT id, name, mode
            FROM games
            WHERE id = @id";
            command.Parameters.AddWithValue("@id", id);

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    game = new DB_Game
                    {
                        id = reader.GetInt32(reader.GetOrdinal("id")),
                        name = reader.GetString(reader.GetOrdinal("name")),
                        mode = reader.GetString(reader.GetOrdinal("mode")),
                    };
                }
            }
        }

        if (game == null)
        {
            Debug.LogWarning($"Game with ID {id} not found.");
        }
        else
        {
            Debug.Log($"Loaded game: {game.name} [ID: {game.id}, Mode: {game.mode}]");
        }

        return game;
    }


    public static int CreateNewGame(string name, string mode, string difficulty, int startingYear)
    {
        int gameId = -1;

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
            INSERT INTO games (name, mode, difficulty, starting_year)
            VALUES (@name, @mode, @difficulty, @starting_year);
            SELECT last_insert_rowid();";

            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@mode", mode);
            command.Parameters.AddWithValue("@difficulty", difficulty);
            command.Parameters.AddWithValue("@starting_year", startingYear);

            object result = command.ExecuteScalar();
            if (result != null && int.TryParse(result.ToString(), out int id))
            {
                gameId = id;
            }
        }

        Debug.Log($"New game created: {name} [{mode}, {difficulty}, {startingYear}] → ID: {gameId}");
        return gameId;
    }

    public static int CreateNewPlayer(string name, int gameId)
    {
        int _id = -1;
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
                INSERT INTO players (name, game_id)
                VALUES (@name, @game_id);
                SELECT last_insert_rowid();
            ";

            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@game_id", gameId);

            object result = command.ExecuteScalar();
            if (result != null && int.TryParse(result.ToString(), out int id))
            {
                _id = id;
            }
        }
        return _id;

    }

    public static int CreateNewNation(string name, string description, int gameId)
    {
        int _id = -1;
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
                INSERT INTO nations (name, description, game_id)
                VALUES (@name, @description, @game_id);
                SELECT last_insert_rowid();
            ";

            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@description", description);
            command.Parameters.AddWithValue("@game_id", gameId);
            
            object result = command.ExecuteScalar();
            print(result);
            if (result != null && int.TryParse(result.ToString(), out int id))
            {
                _id = id;
            }
        }
        return _id;
    }
    public static DBNation GetNationById(int id, int gameId)
    {
        DBNation nation = null;

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
            SELECT * FROM nations WHERE id = @id AND game_id = @game_id";
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@game_id", gameId);

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    nation = new DBNation
                    {
                        id = reader.GetInt32(reader.GetOrdinal("id")),
                        name = reader.GetString(reader.GetOrdinal("name")),
                        description = reader.GetString(reader.GetOrdinal("description")),
                        gameId = reader.GetInt32(reader.GetOrdinal("game_id"))
                    };
                }
            }
        }

        if (nation == null)
        {
            Debug.LogWarning($"Nation with ID {id} not found in Game {gameId}.");
        }
        else
        {
            Debug.Log($"Loaded nation: {nation.name} → Game ID {nation.gameId}");
        }

        return nation;
    }
    public static void DeleteNationsExcept(int preservedId)
    {
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM nations WHERE id != @preservedId";
            command.Parameters.AddWithValue("@preservedId", preservedId);
            command.ExecuteNonQuery();
        }

        Debug.Log($"Deleted all nations except ID {preservedId}.");
    }
    public static void ResetAllTileOwnerships(int gameId)
    {
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "UPDATE tiles SET owner = -1 WHERE game_id = @gameId";
            command.Parameters.AddWithValue("@gameId", gameId);
            command.ExecuteNonQuery();
        }
    }

    public static void MultiplyColumnValues(string tableName, string columnName, double multiplier, int gameId)
    {
        if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(columnName))
        {
            Debug.LogError("Table name or column name cannot be null or empty.");
            return;
        }

        // Basic SQL injection prevention – only allow safe characters
        if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[a-zA-Z_][a-zA-Z0-9_]*$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(columnName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        {
            Debug.LogError("Invalid table or column name.");
            return;
        }

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = $@"
            UPDATE {tableName}
            SET {columnName} = {columnName} * @multiplier
            WHERE game_id = @gameId
        ";

            command.Parameters.AddWithValue("@multiplier", multiplier);
            command.Parameters.AddWithValue("@gameId", gameId);

            try
            {
                int affectedRows = command.ExecuteNonQuery();
                //Debug.Log($"Updated {affectedRows} rows in '{tableName}.{columnName}' for game_id {gameId} (× {multiplier})");
            }
            catch (SqliteException ex)
            {
                Debug.LogError($"SQL Error while updating column '{columnName}' in table '{tableName}': {ex.Message}");
            }
        }
    }

    public static void MultiplyColumnValuesInclusive(string tableName, string targetColumn, double multiplier, int gameId, string conditionColumn, object conditionValue)
    {
        if (string.IsNullOrWhiteSpace(tableName) ||
            string.IsNullOrWhiteSpace(targetColumn) ||
            string.IsNullOrWhiteSpace(conditionColumn))
        {
            Debug.LogError("Table name, target column, or condition column cannot be null or empty.");
            return;
        }

        // Basic SQL injection prevention – only allow safe characters
        string safePattern = @"^[a-zA-Z_][a-zA-Z0-9_]*$";
        if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, safePattern) ||
            !System.Text.RegularExpressions.Regex.IsMatch(targetColumn, safePattern) ||
            !System.Text.RegularExpressions.Regex.IsMatch(conditionColumn, safePattern))
        {
            Debug.LogError("Invalid table or column name.");
            return;
        }

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = $@"
            UPDATE {tableName}
            SET {targetColumn} = {targetColumn} * @multiplier
            WHERE game_id = @gameId AND {conditionColumn} = @conditionValue
        ";

            command.Parameters.AddWithValue("@multiplier", multiplier);
            command.Parameters.AddWithValue("@gameId", gameId);
            command.Parameters.AddWithValue("@conditionValue", conditionValue);

            try
            {
                int affectedRows = command.ExecuteNonQuery();
                Debug.Log($"Updated {affectedRows} rows in '{tableName}.{targetColumn}' where {conditionColumn} = {conditionValue} for game_id {gameId} (× {multiplier})");
            }
            catch (SqliteException ex)
            {
                Debug.LogError($"SQL Error while conditionally updating column '{targetColumn}' in table '{tableName}': {ex.Message}");
            }
        }
    }


    public static void UpdateTileOwnership(int id, int gameId, int newOwner)
    {
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
            UPDATE tiles
            SET owner = @newOwner
            WHERE in_game_id = @id AND game_id = @gameId
        ";

            command.Parameters.AddWithValue("@newOwner", newOwner);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@gameId", gameId);

            try
            {
                int rowsAffected = command.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    Debug.Log($"Tile {id} (Game ID: {gameId}) ownership updated to Nation ID: {newOwner}");
                }
                else
                {
                    Debug.LogWarning($"No tile found with ID {id} for Game {gameId}. Ownership not updated.");
                }
            }
            catch (SqliteException ex)
            {
                Debug.LogError($"Error updating ownership for tile {id} in game {gameId}: {ex.Message}");
            }
        }
    }

    public static void UpdateTilePopulation(int id, int gameId, int population)
    {
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
                UPDATE tiles
                SET population = @newPopulation
                WHERE in_game_id = @id AND game_id = @gameId
            ";
            command.Parameters.AddWithValue("@newPopulation", population);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@gameId", gameId);

            try
            {
                int rowsAffected = command.ExecuteNonQuery();
                if ( rowsAffected > 0)
                {
                    Debug.Log($"Tile {id} (Game ID: {gameId}) population updated to: {population}");
                }
                else
                {
                    Debug.LogWarning($"No tile found with ID {id} for game {gameId}. Population not updated.");
                }
            }
            catch (SqliteException ex)
            {
                Debug.LogError($"Error updating population for tile {id} in game {gameId}: {ex.Message}");
            }
        }
    }

    public static int GetSumOfColumn(int gameId, string columnName)
    {
        int sum = 0;

        if (string.IsNullOrWhiteSpace(columnName))
        {
            Debug.LogError("Column name cannot be null or empty.");
            return sum;
        }

        // Prevent SQL injection by validating the column name
        if (!System.Text.RegularExpressions.Regex.IsMatch(columnName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        {
            Debug.LogError("Invalid column name.");
            return sum;
        }

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = $@"
            SELECT SUM({columnName}) FROM tiles
            WHERE game_id = @gameId
        ";

            command.Parameters.AddWithValue("@gameId", gameId);

            try
            {
                object result = command.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    sum = Convert.ToInt32(result);
                }
            }
            catch (SqliteException ex)
            {
                Debug.LogError($"SQL Error while summing column '{columnName}' in 'tiles': {ex.Message}");
            }
        }

        Debug.Log($"Sum of '{columnName}' in game {gameId} = {sum}");
        return sum;
    }
    
    public static List<int> GetNationIdsByGameId(int gameId)
    {
        List<int> nationIds = new List<int>();

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
            SELECT id FROM nations
            WHERE game_id = @gameId
        ";
            command.Parameters.AddWithValue("@gameId", gameId);

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    nationIds.Add(reader.GetInt32(0));
                }
            }
        }

        return nationIds;
    }

    public static void UpdateNationIncrements(string table, string column, Dictionary<int, double> nationMultipliers, int gameId)
    {
        if (nationMultipliers == null || nationMultipliers.Count == 0)
        {
            Debug.LogWarning("No nation multipliers provided.");
            return;
        }

        if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(column))
        {
            Debug.LogError("Table or column name is null/empty.");
            return;
        }

        // Validate table/column names
        string safePattern = @"^[a-zA-Z_][a-zA-Z0-9_]*$";
        if (!System.Text.RegularExpressions.Regex.IsMatch(table, safePattern) ||
            !System.Text.RegularExpressions.Regex.IsMatch(column, safePattern))
        {
            Debug.LogError("Invalid table or column name.");
            return;
        }

        using (var command = _connection.CreateCommand())
        {
            // Build CASE statement
            List<string> cases = new List<string>();
            List<string> ownerIds = new List<string>();
            foreach (var pair in nationMultipliers)
            {
                int nationId = pair.Key;
                double multiplier = pair.Value;

                string caseClause = $"WHEN {nationId} THEN {multiplier.ToString("0.######")}";
                cases.Add(caseClause);
                ownerIds.Add(nationId.ToString());
            }

            string caseStatement = $"CASE owner\n    {string.Join("\n    ", cases)}\n    ELSE 1.0\nEND";
            string inClause = string.Join(",", ownerIds);

            command.CommandText = $@"
            UPDATE {table}
            SET {column} = {column} * {caseStatement}
            WHERE game_id = @gameId AND owner IN ({inClause});
        ";

            command.Parameters.AddWithValue("@gameId", gameId);

            try
            {
                int rowsAffected = command.ExecuteNonQuery();
                Debug.Log($"Batch population update complete. Rows affected: {rowsAffected}");
            }
            catch (SqliteException ex)
            {
                Debug.LogError($"SQL Error during batch update: {ex.Message}");
            }
        }
    }


}

// Model Structures
namespace DBModels
{

    /// <summary>
    /// High Level Representation of a Game Tile.
    /// </summary>
    public class DBTile
    {
        public int dbId; // optional: filled only when pulling from DB
        public int gameId; // unique per game
        public int gameRefId; // FK to games.id
        public string name;
        public string description;
        public string type;

        public int population;
        public int infrastructureRating;
        public int factories;
        public int stability;
        public int owner = 0; // the id of the nation that owns this tile.

        public List<int> neighborGameIds = new(); // game IDs of neighboring tiles
    }

    /// <summary>
    /// High Level representation of a game.
    /// </summary>
    public class DB_Game
    {
        public int id;
        public string name;
        public string mode;
        public string difficulty;
        public int startingYear;
    }

    public class DBNation
    {
        public int id;
        public string name;
        public string description;
        public int gameId;
    }

}

