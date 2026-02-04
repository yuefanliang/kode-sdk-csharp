# Python Code Audit Checklist

## Code Quality

### Naming Conventions (PEP 8)
- [ ] Classes use PascalCase (e.g., `UserService`)
- [ ] Functions and variables use snake_case (e.g., `get_user_by_id`)
- [ ] Constants use UPPER_SNAKE_CASE (e.g., `MAX_RETRY_COUNT`)
- [ ] Private members use leading underscore (e.g., `_internal_method`)
- [ ] Modules use lowercase with underscores (e.g., `user_service.py`)

### Code Structure
- [ ] Follow PEP 8 style guide
- [ ] Proper module organization
- [ ] Single Responsibility Principle applied
- [ ] DRY (Don't Repeat Yourself) principle followed
- [ ] Meaningful variable and function names

### Documentation
- [ ] Docstrings for modules
- [ ] Docstrings for classes
- [ ] Docstrings for functions and methods
- [ ] Parameters documented in docstrings
- [ ] Return values documented in docstrings

## Security

### Input Validation
- [ ] All user inputs are validated
- [ ] SQL injection prevention (use parameterized queries)
- [ ] XSS prevention (proper output encoding)
- [ ] CSRF protection enabled
- [ ] File upload validation

### Authentication & Authorization
- [ ] Passwords properly hashed (bcrypt, argon2)
- [ ] Session management implemented
- [ ] Role-based access control
- [ ] Secure authentication mechanisms
- [ ] Proper logout implementation

### Data Protection
- [ ] Sensitive data encrypted at rest
- [ ] Sensitive data encrypted in transit (HTTPS)
- [ ] No hardcoded secrets or passwords
- [ ] Proper error handling (no information leakage)
- [ ] Secure configuration management (using environment variables)

## Performance

### Database
- [ ] Proper indexing on frequently queried columns
- [ ] Connection pooling configured
- [ ] N+1 query problem avoided
- [ ] Batch operations for bulk inserts/updates
- [ ] Query optimization

### Memory Management
- [ ] No memory leaks
- [ ] Proper resource cleanup (context managers)
- [ ] Efficient data structures used
- [ ] Generator expressions used where appropriate
- [ ] Caching strategy implemented

### Concurrency
- [ ] Thread safety considered
- [ ] Proper synchronization mechanisms
- [ ] Async/await used appropriately
- [ ] Race conditions avoided
- [ ] Proper use of concurrent.futures

## Testing

### Unit Tests
- [ ] Unit tests for all public functions
- [ ] Test coverage > 80%
- [ ] Edge cases tested
- [ ] Mock objects used appropriately
- [ ] Test naming follows conventions

### Integration Tests
- [ ] API endpoints tested
- [ ] Database operations tested
- [ ] External service integrations tested
- [ ] End-to-end scenarios tested
- [ ] Test data properly managed

### Code Analysis Tools
- [ ] pylint configured and passing
- [ ] flake8 configured and passing
- [ ] black configured for formatting
- [ ] mypy for type checking
- [ ] bandit for security scanning

## Best Practices

### Exception Handling
- [ ] Specific exceptions caught
- [ ] Proper exception chaining
- [ ] Meaningful error messages
- [ ] No bare except clauses
- [ ] Resources cleaned up with context managers

### Logging
- [ ] Proper logging framework used (logging module)
- [ ] Appropriate log levels (DEBUG, INFO, WARNING, ERROR, CRITICAL)
- [ ] No sensitive data in logs
- [ ] Structured logging for better analysis
- [ ] Log rotation configured

### Dependencies
- [ ] Dependencies up to date
- [ ] No known security vulnerabilities (checked with safety)
- [ ] Proper dependency management (requirements.txt, pyproject.toml)
- [ ] Transitive dependencies reviewed
- [ ] License compliance checked

### Type Hints
- [ ] Type hints used for function parameters
- [ ] Type hints used for return values
- [ ] Proper use of typing module
- [ ] Type checking with mypy
- [ ] Consistent type annotation style

## Framework Specific

### Django
- [ ] Models properly defined
- [ ] Migrations created and applied
- [ ] Views follow best practices
- [ ] URLs properly configured
- [ ] Middleware properly configured
- [ ] Settings properly externalized
- [ ] Admin interface configured
- [ ] Forms properly validated

### Flask
- [ ] Blueprints used for organization
- [ ] Configuration properly externalized
- [ ] Extensions properly initialized
- [ ] Request validation implemented
- [ ] Error handling configured
- [ ] Database properly configured
- [ ] CSRF protection enabled
- [ ] Template context processors

### FastAPI
- [ ] Pydantic models for validation
- [ ] Async/await used appropriately
- [ ] Dependency injection used
- [ ] API documentation generated
- [ ] Middleware properly configured
- [ ] CORS properly configured
- [ ] Background tasks properly handled
- [ ] WebSocket endpoints (if applicable)

## General Python Best Practices

### Code Style
- [ ] Follow PEP 8 guidelines
- [ ] Use black for formatting
- [ ] Use isort for import sorting
- [ ] Line length <= 79 characters (or 88 with black)
- [ ] Proper import ordering

### Pythonic Code
- [ ] List comprehensions used appropriately
- [ ] Context managers used for resource management
- [ ] Decorators used appropriately
- [ ] Generator functions used for large datasets
- [ ] Built-in functions used (map, filter, etc.)

### Error Handling
- [ ] Custom exceptions defined
- [ ] Exception hierarchy properly designed
- [ ] Logging in exception handlers
- [ ] Graceful degradation
- [ ] User-friendly error messages

## Security Tools

- [ ] bandit for security scanning
- [ ] safety for dependency vulnerability checking
- [ ] pylint for code analysis
- [ ] flake8 for style checking
- [ ] mypy for type checking
