# JavaScript/TypeScript Code Audit Checklist

## Code Quality

### Naming Conventions
- [ ] Classes use PascalCase (e.g., `UserService`)
- [ ] Functions and variables use camelCase (e.g., `getUserById`)
- [ ] Constants use UPPER_SNAKE_CASE (e.g., `MAX_RETRY_COUNT`)
- [ ] Files use kebab-case (e.g., `user-service.js`)
- [ ] Components use PascalCase (e.g., `UserProfile.tsx`)

### Code Structure
- [ ] Proper module organization
- [ ] Single Responsibility Principle applied
- [ ] DRY (Don't Repeat Yourself) principle followed
- [ ] Meaningful variable and function names
- [ ] Proper file and folder structure

### Documentation
- [ ] JSDoc comments for functions
- [ ] JSDoc comments for classes
- [ ] Parameters documented in JSDoc
- [ ] Return values documented in JSDoc
- [ ] Complex logic explained with comments

## Security

### Input Validation
- [ ] All user inputs are validated
- [ ] XSS prevention (proper output encoding)
- [ ] CSRF protection enabled
- [ ] Content Security Policy configured
- [ ] File upload validation

### Authentication & Authorization
- [ ] Passwords properly hashed (bcrypt, argon2)
- [ ] JWT properly implemented
- [ ] Session management implemented
- [ ] Role-based access control
- [ ] Secure authentication mechanisms

### Data Protection
- [ ] Sensitive data encrypted at rest
- [ ] Sensitive data encrypted in transit (HTTPS)
- [ ] No hardcoded secrets or passwords
- [ ] Proper error handling (no information leakage)
- [ ] Secure configuration management (using environment variables)

## Performance

### Frontend
- [ ] Code splitting implemented
- [ ] Lazy loading for images and components
- [ ] Tree shaking enabled
- [ ] Minification configured
- [ ] Bundle size optimized

### Backend
- [ ] Proper indexing on frequently queried columns
- [ ] Connection pooling configured
- [ ] N+1 query problem avoided
- [ ] Caching strategy implemented
- [ ] Query optimization

### Memory Management
- [ ] No memory leaks
- [ ] Proper cleanup of event listeners
- [ ] Efficient data structures used
- [ ] Proper cleanup of timers and intervals
- [ ] Web Workers for CPU-intensive tasks

## Testing

### Unit Tests
- [ ] Unit tests for all functions
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
- [ ] ESLint configured and passing
- [ ] Prettier configured for formatting
- [ ] TypeScript strict mode enabled (if applicable)
- [ ] Husky for pre-commit hooks
- [ ] Lint-staged for linting staged files

## Best Practices

### Error Handling
- [ ] Specific errors caught
- [ ] Proper error chaining
- [ ] Meaningful error messages
- [ ] No silent failures
- [ ] Global error handler configured

### Logging
- [ ] Proper logging framework used (winston, pino)
- [ ] Appropriate log levels (debug, info, warn, error)
- [ ] No sensitive data in logs
- [ ] Structured logging for better analysis
- [ ] Log rotation configured

### Dependencies
- [ ] Dependencies up to date
- [ ] No known security vulnerabilities (checked with npm audit)
- [ ] Proper dependency management (package.json)
- [ ] Transitive dependencies reviewed
- [ ] License compliance checked

### TypeScript (if applicable)
- [ ] Strict mode enabled
- [ ] Proper type definitions
- [ ] No `any` types used
- [ ] Proper interface definitions
- [ ] Generic types used appropriately

## Framework Specific

### React
- [ ] Components properly structured
- [ ] Hooks used appropriately
- [ ] State management implemented (Redux, Context API, etc.)
- [ ] Props validation (PropTypes or TypeScript)
- [ ] Proper use of useEffect
- [ ] Performance optimization (useMemo, useCallback)
- [ ] Error boundaries implemented
- [ ] Proper key usage in lists

### Vue
- [ ] Components properly structured
- [ ] Composition API used appropriately
- [ ] State management implemented (Vuex, Pinia)
- [ ] Props validation implemented
- [ ] Proper lifecycle hook usage
- [ ] Performance optimization (computed, watch)
- [ ] Error handling implemented
- [ ] Proper component communication

### Node.js/Express
- [ ] Middleware properly configured
- [ ] Route handlers properly organized
- [ ] Error handling middleware
- [ ] Security middleware (helmet, cors)
- [ ] Proper async/await usage
- [ ] Request validation implemented
- [ ] Rate limiting configured
- [ ] Proper logging middleware

## General JavaScript Best Practices

### Code Style
- [ ] ESLint configured and passing
- [ ] Prettier configured for formatting
- [ ] Consistent code style
- [ ] Proper import ordering
- [ ] No unused imports or variables

### Modern JavaScript
- [ ] const and let used (no var)
- [ ] Arrow functions used appropriately
- [ ] Template literals used
- [ ] Destructuring used appropriately
- [ ] Spread operator used appropriately
- [ ] Async/await used (no callbacks)
- [ ] Optional chaining used
- [ ] Nullish coalescing used

### Error Handling
- [ ] Custom errors defined
- [ ] Proper error types
- [ ] Error boundaries (in React)
- [ ] Global error handlers
- [ ] User-friendly error messages

## Security Tools

- [ ] npm audit for vulnerability checking
- [ ] Snyk for security scanning
- [ ] ESLint with security plugins
- [ ] Prettier for code formatting
- [ ] TypeScript for type safety (if applicable)
