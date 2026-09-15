; Analyzer release tracking — unshipped release (RS2008).
;
; Every DiagnosticDescriptor declared in PacketHandlerRegistryGenerator.cs must
; have a row here, and the Category/Severity columns must match that descriptor
; exactly. RS2000 fires for a missing row and RS2001 for a stale one; both are
; warnings that this project promotes to errors via TreatWarningsAsErrors.
;
; Table shape is fixed by the parser, not by Markdown: the header must be the
; bare "Rule ID | Category | Severity | Notes" form with no surrounding pipes,
; because analyzer packages older than 4.x reject a leading '|'. Notes must not
; contain a '|' character, which would be read as an extra column.
;
; server-cs/tools/verify_server_layout.py cross-checks these rows against the
; generator source without needing a .NET SDK.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PMH001 | PaperMan.HandlerDiscovery | Error | PacketHandlerRegistryGenerator: a required Protocol or raw-marker symbol is unavailable
PMH002 | PaperMan.HandlerDiscovery | Error | PacketHandlerRegistryGenerator: a canonical-name or raw-marked method has the wrong static handler shape
PMH003 | PaperMan.HandlerDiscovery | Error | PacketHandlerRegistryGenerator: two methods claim the same numeric opcode
PMH004 | PaperMan.HandlerDiscovery | Error | PacketHandlerRegistryGenerator: a raw marker is malformed or applied to a named opcode
PMH005 | PaperMan.HandlerDiscovery | Error | PacketHandlerRegistryGenerator: a receive-shaped method uses an ACK or base catalog token
