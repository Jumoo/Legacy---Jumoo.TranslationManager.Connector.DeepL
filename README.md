# DeepL Translation Connector for Translation Manager

> **⚠️ IMPORTANT: LEGACY CODE NOTICE**
>
> **This is legacy code for Umbraco v8 only. This code is unsupported and provided as-is without any warranty, express or implied. Use at your own risk.**
>
> We offer no support, updates, or guarantees regarding the functionality, security, or compatibility of this code. It is provided solely for historical reference and for those who may still be maintaining legacy Umbraco v8 installations.

---

## Overview

The DeepL Translation Connector is a plugin for Translation Manager that integrates with the [DeepL Translation API](https://www.deepl.com/pro-api) to provide automated machine translation capabilities for Umbraco v8 content.

This connector allows you to automatically translate content in your Umbraco CMS using DeepL's neural machine translation technology, supporting multiple languages and content types.

## Features

- **Machine Translation**: Automatic content translation using DeepL's neural translation API
- **Multi-Language Support**: Access to all languages supported by the DeepL API
- **HTML Content Support**: Intelligent handling of HTML content with option to split and translate segments
- **Free and Pro API Support**: Compatible with both DeepL Free and Pro API accounts
- **Throttling Control**: Configurable delay between API calls to manage rate limits
- **Backoffice Integration**: Full integration with Translation Manager's Umbraco backoffice interface

## Requirements

- Umbraco v8.4.2 or higher
- Translation Manager Core v9.1.5 or higher
- .NET Framework 4.7.2
- Valid DeepL API key (Free or Pro)

## Installation

Install via NuGet Package Manager:

```bash
Install-Package Jumoo.TranslationManager.Connector.DeepL
```

Or via .NET CLI:

```bash
dotnet add package Jumoo.TranslationManager.Connector.DeepL
```

## Configuration

After installation, configure the connector in the Translation Manager backoffice:

1. Navigate to the Translation Manager section in Umbraco
2. Go to Settings > Translation Connectors
3. Select "DeepL Translation Connector v2 (Machine)"
4. Enter your DeepL API Key
5. Select API type (Free or Pro)
6. Configure optional settings:
   - **Throttle**: Delay between API calls in milliseconds
   - **Split HTML**: Enable to split HTML content for better translation
   - **Treat as HTML**: Parse and translate HTML content intelligently

## Usage

Once configured, the DeepL connector will be available as a translation provider when creating translation jobs in Translation Manager. The connector automatically:

- Detects supported source and target languages
- Translates content properties
- Handles nested content structures
- Manages HTML and rich text content

## Dependencies

- DeepL.net (v1.17.0)
- Jumoo.TranslationManager.Core (v9.1.5)
- UmbracoCms.Web (v8.4.2)

## Technical Details

The connector consists of two main components:

- **DeepLConnector**: Implements the `ITranslationProvider` interface and manages translation job submission
- **DeepLTranslationService**: Handles communication with the DeepL API

## License

This package targets Umbraco v8 which is no longer supported. Use this code at your own risk.

## Support

**No support is provided for this legacy code.**

For current versions of Translation Manager and supported connectors, please visit [https://jumoo.co.uk/translate](https://jumoo.co.uk/translate)
