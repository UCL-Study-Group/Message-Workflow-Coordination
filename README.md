# Workflow Coordination with RabbitMQ and PostgreSQL

This project demonstrates the Request-Reply pattern using RabbitMQ for message coordination between a workflow coordinator and worker processes, with PostgreSQL for state management.

## Test database commands

### 1. Start PostgreSQL Docker Container

```bash
docker run --name postgres -e POSTGRES_PASSWORD=mysecretpassword -p 5432:5432 -d postgres
```

### 2. Create Database

```bash
docker exec -it postgres psql -U postgres -c "CREATE DATABASE workflowdb;"
```

### 3. Create Table and Insert Test Data

```bash
docker exec -it postgres psql -U postgres -d workflowdb -c "
  CREATE TABLE work_items (
    id SERIAL PRIMARY KEY,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    data TEXT,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    result TEXT
  );
  
  INSERT INTO work_items (status, data) VALUES 
    ('Pending', 'Process customer order #1001'),
    ('Pending', 'Generate monthly report'),
    ('Pending', 'Send notification emails');
"
```

### 4. Verify Test Data (Optional)

```bash
docker exec -it postgres psql -U postgres -d workflowdb -c "SELECT * FROM work_items;"
```


## Useful Database Commands

### Reset All Work Items to Pending

```bash
docker exec -it postgres psql -U postgres -d workflowdb -c "UPDATE work_items SET status = 'Pending', started_at = NULL, completed_at = NULL, result = NULL;"
```

### Reset Specific Work Item to Pending

```bash
docker exec -it postgres psql -U postgres -d workflowdb -c "UPDATE work_items SET status = 'Pending', started_at = NULL, completed_at = NULL, result = NULL WHERE id = 1;"
```

### View All Work Items

```bash
docker exec -it postgres psql -U postgres -d workflowdb -c "SELECT * FROM work_items;"
```

### View Only Pending Work Items

```bash
docker exec -it postgres psql -U postgres -d workflowdb -c "SELECT * FROM work_items WHERE status = 'Pending';"
```

## Troubleshooting (Errors that quite easily can be encountered again)

### "Work item not available" Error

The work item is not in 'Pending' status. Reset it using the database commands above.
