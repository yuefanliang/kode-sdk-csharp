# Code Audit & Testing Skill - Quick Start Guide

## Overview
This skill provides automated code review, testing, and report generation capabilities for various programming languages and frameworks.

## Getting Started

### 1. Activate the Skill
```bash
skill_activate code-audit
```

### 2. Basic Usage

#### Analyze a Project
```
"Please audit the project at /path/to/project"
```

#### Run Tests and Generate Report
```
"Run tests on my Java project and generate a report"
```

#### Code Quality Check
```
"Check code quality of my Python project"
```

#### Security Scan
```
"Perform a security scan on my Node.js application"
```

## Supported Project Types

### Java Projects
- Maven projects (pom.xml detected)
- Gradle projects (build.gradle detected)
- Spring Boot applications
- Standard Java applications

### Python Projects
- pip projects (requirements.txt detected)
- Poetry projects (pyproject.toml detected)
- Django applications
- Flask applications
- FastAPI applications

### JavaScript/TypeScript Projects
- npm projects (package.json detected)
- yarn projects (yarn.lock detected)
- React applications
- Vue applications
- Node.js/Express applications

### C#/.NET Projects
- .NET Core projects (.csproj detected)
- ASP.NET Core applications
- Console applications
- Class libraries

## What the Skill Does

### Phase 1: Project Analysis
- Identifies project type and technology stack
- Lists all source files and directories
- Analyzes dependencies
- Detects build system

### Phase 2: Code Quality Analysis
- Runs static code analysis tools
- Checks for code smells and anti-patterns
- Analyzes code complexity
- Reviews code structure

### Phase 3: Security Analysis
- Scans for security vulnerabilities
- Checks for hardcoded secrets
- Reviews authentication/authorization
- Analyzes dependency security

### Phase 4: Automated Testing
- Runs unit tests
- Runs integration tests
- Measures test coverage
- Analyzes test results

### Phase 5: Report Generation
- Collects all metrics and findings
- Generates comprehensive report
- Prioritizes issues by severity
- Provides actionable recommendations

## Output

The skill generates:
1. **Test Report** (Markdown) - Comprehensive analysis report
2. **Findings Summary** - List of issues grouped by severity
3. **Recommendations** - Action items for improvement
4. **Metrics** - Code quality and coverage metrics

## Configuration Options

You can customize the analysis by specifying:
- **Project Path**: Path to the project directory
- **Analysis Depth**: Quick scan vs. deep analysis
- **Report Format**: Markdown (default), HTML, or JSON
- **Output Location**: Where to save the report
- **Severity Threshold**: Minimum severity level to report

## Example Workflows

### Example 1: Java Spring Boot Project
```
User: "Audit my Spring Boot project"
Skill:
1. Detects Maven + Spring Boot
2. Runs mvn checkstyle:check
3. Runs mvn pmd:check
4. Runs mvn test
5. Generates comprehensive report
```

### Example 2: React Application
```
User: "Review my React app"
Skill:
1. Detects npm + React
2. Runs npm audit
3. Runs npm test
4. Runs ESLint
5. Generates report with findings
```

### Example 3: Python Django Project
```
User: "Check code quality of my Django project"
Skill:
1. Detects pip + Django
2. Runs pylint
3. Runs pytest
4. Runs bandit for security
5. Generates quality report
```

## Best Practices

1. **Run analysis regularly** - Integrate into CI/CD pipeline
2. **Address critical issues first** - Prioritize P0 and P1 issues
3. **Improve test coverage** - Aim for >80% coverage
4. **Keep dependencies updated** - Regular security updates
5. **Follow coding standards** - Consistent code style
6. **Document your code** - Improve maintainability
7. **Review findings** - Understand and address issues
8. **Track progress** - Monitor improvements over time

## Troubleshooting

### Build Tool Not Found
- Ensure the build tool is installed (Maven, npm, Python, etc.)
- Add the tool to your system PATH
- Provide installation instructions in the error message

### Tests Fail
- Check test logs for detailed error messages
- Verify test environment is properly configured
- Check for missing dependencies
- Review test configuration

### Analysis Timeout
- Break analysis into smaller tasks
- Increase timeout if analyzing large projects
- Focus on critical areas first
- Use quick scan mode for initial analysis

### Project Type Not Recognized
- The skill will attempt generic analysis
- Provide more details about the project
- Specify language and framework manually
- Check for standard project files (pom.xml, package.json, etc.)

## Tips for Better Results

1. **Provide clear project path** - Ensure path is correct and accessible
2. **Specify analysis type** - Choose between quick scan or deep analysis
3. **Review tool configuration** - Ensure analysis tools are properly configured
4. **Check dependencies** - Ensure all dependencies are installed
5. **Clean build artifacts** - Remove old build files before analysis
6. **Use version control** - Analyze specific commits or branches
7. **Compare results** - Track improvements over time
8. **Integrate with CI/CD** - Automate analysis in your pipeline

## Additional Resources

- **Java Checklist**: references/java-checklist.md
- **Python Checklist**: references/python-checklist.md
- **JavaScript Checklist**: references/javascript-checklist.md
- **Report Template**: assets/report-template.md

## Support

For issues or questions:
1. Check the troubleshooting section
2. Review the error messages and logs
3. Verify project configuration
4. Contact the development team

---

**Last Updated**: 2026-01-28
**Version**: 1.0.0
