# Code Audit & Testing Skill

## Skill Description
Automated code review, testing, and report generation workflow. Use when user needs to:
- Perform code quality checks and analysis
- Run automated tests on a codebase
- Generate comprehensive test reports
- Audit project structure and dependencies
- Check for security vulnerabilities
- Analyze code metrics and complexity

## Workflow Overview

### Phase 1: Project Analysis
1. **Explore Project Structure**
   - Identify project type (Java, Python, JavaScript, C#, etc.)
   - List source files and directories
   - Identify configuration files
   - Detect build systems (Maven, Gradle, npm, etc.)

2. **Analyze Dependencies**
   - List all dependencies
   - Check for outdated dependencies
   - Identify security vulnerabilities
   - Check for license compliance

### Phase 2: Code Quality Analysis
1. **Static Code Analysis**
   - Run linters and code style checkers
   - Check for code smells and anti-patterns
   - Analyze code complexity
   - Check for duplicated code

2. **Security Analysis**
   - Check for hardcoded secrets
   - Scan for common security issues
   - Analyze permission configurations
   - Review authentication/authorization

3. **Architecture Review**
   - Analyze code structure and organization
   - Check design patterns usage
   - Review separation of concerns
   - Evaluate scalability and maintainability

### Phase 3: Automated Testing
1. **Unit Testing**
   - Identify existing test files
   - Run unit tests
   - Check test coverage
   - Analyze test results

2. **Integration Testing**
   - Run integration tests
   - Test API endpoints
   - Test database operations
   - Verify service integrations

3. **Build Verification**
   - Run build process
   - Check for compilation errors
   - Verify build artifacts
   - Test deployment readiness

### Phase 4: Report Generation
1. **Collect Metrics**
   - Code quality scores
   - Test coverage percentages
   - Security vulnerability counts
   - Performance metrics

2. **Generate Report**
   - Create comprehensive test report
   - Include findings and recommendations
   - Prioritize issues by severity
   - Provide action items

3. **Export Results**
   - Generate Markdown report
   - Create JSON summary
   - Export detailed logs
   - Save test artifacts

## Supported Languages & Frameworks

### Java
- Build Tools: Maven, Gradle
- Linters: Checkstyle, PMD, SpotBugs
- Testing: JUnit, TestNG, Mockito
- Security: OWASP Dependency-Check

### Python
- Package Managers: pip, poetry
- Linters: pylint, flake8, black
- Testing: pytest, unittest, nose2
- Security: bandit, safety

### JavaScript/TypeScript
- Package Managers: npm, yarn, pnpm
- Linters: ESLint, TSLint, Prettier
- Testing: Jest, Mocha, Jasmine
- Security: npm audit, yarn audit

### C#/.NET
- Build Tools: dotnet CLI, MSBuild
- Linters: StyleCop, ReSharper
- Testing: xUnit, NUnit, MSTest
- Security: Security Code Scan

## Output Format

### Report Structure
```markdown
# Code Audit & Testing Report

## 1. Project Overview
- Project name and type
- Technology stack
- Analysis date

## 2. Code Quality Analysis
- Static analysis results
- Code metrics
- Quality scores

## 3. Security Analysis
- Vulnerability findings
- Security recommendations
- Compliance status

## 4. Test Results
- Unit test results
- Integration test results
- Coverage metrics

## 5. Findings Summary
- Critical issues
- High priority issues
- Medium priority issues
- Low priority issues

## 6. Recommendations
- Short-term improvements
- Long-term improvements
- Best practices

## 7. Conclusion
- Overall assessment
- Approval status
- Next steps
```

## Usage Instructions

### Trigger Conditions
Use this skill when user mentions:
- "代码审查" (code review)
- "代码分析" (code analysis)
- "自动化测试" (automated testing)
- "测试报告" (test report)
- "代码质量" (code quality)
- "安全扫描" (security scan)
- "项目审计" (project audit)

### Workflow Steps
1. Ask user for project path if not provided
2. Analyze project structure and identify language/framework
3. Run appropriate analysis tools for the detected stack
4. Execute automated tests if available
5. Collect and analyze results
6. Generate comprehensive report
7. Save report to project directory

### Configuration
The skill can be configured via:
- **Project Path**: Root directory of the project to analyze
- **Analysis Depth**: Quick scan vs. deep analysis
- **Report Format**: Markdown, HTML, JSON
- **Output Location**: Where to save the generated report
- **Severity Threshold**: Minimum severity level to report

## Tools Used

### Built-in Tools
- `fs_list`: Explore directory structure
- `fs_read`: Read source code files
- `fs_glob`: Find files matching patterns
- `fs_grep`: Search for patterns in code
- `bash_run`: Execute analysis commands
- `fs_write`: Generate reports

### External Tools (when available)
- Maven/Gradle (Java)
- npm/yarn (JavaScript)
- pytest (Python)
- dotnet (C#)
- Various linters and analyzers

## Best Practices

1. **Always** ask for confirmation before making any changes to the codebase
2. **Never** execute destructive operations without explicit permission
3. **Always** provide clear explanations of findings
4. **Prioritize** security vulnerabilities and critical issues
5. **Generate** actionable recommendations with specific steps
6. **Respect** project-specific coding standards
7. **Handle** large projects efficiently by focusing on critical areas
8. **Maintain** confidentiality of sensitive information found during analysis

## Error Handling

- If build tools are not found, provide installation instructions
- If tests fail, provide detailed error messages and debugging tips
- If project type is not recognized, offer to perform generic analysis
- If analysis timeout occurs, suggest breaking into smaller tasks

## Example Scenarios

### Scenario 1: Java Project Audit
User: "Please audit the Spring Boot project at /path/to/project"
Skill: 
1. Detects Java + Spring Boot + Maven
2. Runs `mvn checkstyle:check`
3. Runs `mvn pmd:check`
4. Runs `mvn test`
5. Generates comprehensive report

### Scenario 2: JavaScript Project Review
User: "Review the React project and generate a test report"
Skill:
1. Detects JavaScript + React + npm
2. Runs `npm audit`
3. Runs `npm test`
4. Analyzes code with ESLint
5. Generates report with findings

### Scenario 3: Python Code Quality Check
User: "Check code quality of my Python project"
Skill:
1. Detects Python project
2. Runs `pylint` on source files
3. Runs `pytest` for tests
4. Checks for security issues with `bandit`
5. Provides quality score and recommendations
