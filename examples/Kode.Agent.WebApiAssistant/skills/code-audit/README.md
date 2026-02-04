# Code Audit & Testing Skill

## Description
A comprehensive skill for automated code review, testing, and report generation across multiple programming languages and frameworks.

## Features

- **Multi-language Support**: Java, Python, JavaScript/TypeScript, C#/.NET
- **Automated Analysis**: Static code analysis, security scanning, dependency checking
- **Test Execution**: Unit tests, integration tests, coverage measurement
- **Report Generation**: Comprehensive Markdown reports with findings and recommendations
- **Security Scanning**: Vulnerability detection, security best practices review
- **Code Quality Metrics**: Coverage, complexity, duplication, technical debt

## Installation

The skill is located in the `code-audit` directory. To use it:

1. Ensure the skill is in your skills directory
2. Activate the skill: `skill_activate code-audit`
3. Follow the quick start guide

## Usage

### Basic Commands

```
"Please audit the project at /path/to/project"
"Run tests on my Java project and generate a report"
"Check code quality of my Python project"
"Perform a security scan on my Node.js application"
```

### Supported Languages

- **Java**: Maven, Gradle, Spring Boot
- **Python**: pip, Poetry, Django, Flask, FastAPI
- **JavaScript/TypeScript**: npm, yarn, React, Vue, Node.js
- **C#/.NET**: .NET Core, ASP.NET Core

## Skill Structure

```
code-audit/
├── SKILL.md                      # Main skill definition
├── README.md                     # This file
├── references/                   # Reference materials
│   ├── java-checklist.md        # Java audit checklist
│   ├── python-checklist.md      # Python audit checklist
│   ├── javascript-checklist.md  # JavaScript/TypeScript checklist
│   └── quick-start.md           # Quick start guide
└── assets/                       # Asset files
    └── report-template.md        # Report generation template
```

## Workflow

1. **Project Analysis**: Detect project type and structure
2. **Code Quality Analysis**: Run static analysis tools
3. **Security Analysis**: Scan for vulnerabilities
4. **Automated Testing**: Execute tests and measure coverage
5. **Report Generation**: Create comprehensive report

## Output

The skill generates:
- Comprehensive test report (Markdown)
- Findings summary grouped by severity
- Actionable recommendations
- Code quality and coverage metrics

## Configuration

Customize analysis by specifying:
- Project path
- Analysis depth (quick vs. deep)
- Report format (Markdown, HTML, JSON)
- Output location
- Severity threshold

## Best Practices

1. Run analysis regularly
2. Address critical issues first
3. Improve test coverage (>80%)
4. Keep dependencies updated
5. Follow coding standards
6. Document your code

## Contributing

To extend this skill:
1. Add new language checklists in `references/`
2. Update the report template in `assets/`
3. Modify the workflow in `SKILL.md`
4. Test with various project types

## License

This skill is part of the Kode SDK C# project.

## Support

For issues or questions, refer to the quick start guide or contact the development team.

---

**Version**: 1.0.0
**Last Updated**: 2026-01-28
