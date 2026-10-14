namespace SecretImport.Core;

public enum ImportMode { Overwrite, Skip, Replace }

public enum SecretChangeKind { Added, Changed, Unchanged, Removed, Skipped }

public enum SecretsIdSource { None, MsBuild, XmlFallback }

public enum ImportWarningKind { NullValueSkipped, SharedSecretsId, XmlFallbackUsed, InsecureFilePermissions, MissingProjectFile }

public enum ImportErrorKind { InvalidInput, NoProjectFound, MultipleProjects, SdkUnavailable, ConcurrentModification, IoFailure }
