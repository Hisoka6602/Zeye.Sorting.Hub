export interface AccessSession { configured: boolean; bootstrapAvailable: boolean; authenticated: boolean; isSuperAdministrator: boolean; enforceAuthorization: boolean; name?: string; avatarUrl?: string | null; permissions: string[] }
export interface AccessProfile { id: string; account: string; name: string; roleName: string; lastLogin?: string; email: string; phone: string; bio: string; avatarUrl?: string | null; revision: number; directoryRevision: number }
export interface AccessRole { id: string; name: string; description: string; permissions: string[]; builtIn: boolean; modified: string; members: number }
export interface AccessUser { id: string; account: string; name: string; roleId: string; enabled: boolean; builtIn: boolean; lastLogin?: string }
export interface AccessDirectory { configured: boolean; revision: number; roles: AccessRole[]; users: AccessUser[]; permissions: { code: string; label: string }[] }
