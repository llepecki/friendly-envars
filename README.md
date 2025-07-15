# FriendlyEnvars

FriendlyEnvars is a simple replacement for native .NET configuration.

The library provides configuration ingestion from environment variables (only!) to strongly typed dtos and is compatible with IOptions interface.

The library handles biding for most common primitives and allows to extend it to handle custom types.

## TODO

1. Enable users to set culture
2. Add support for all primitive types  
3. Provide a way to extend the binder with user custom types (takes priority over default binder)