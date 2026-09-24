export interface LoginDto {
  username: string;
  password: string;
}

export interface AuthResponseDto {
  token: string;
  role: string;
  username: string;
  fullName: string;
  userId: string;
  access: UserAccessCapabilitiesDto;
}

export interface CurrentUserDto {
  role: string;
  username: string;
  fullName: string;
  userId: string;
  access: UserAccessCapabilitiesDto;
}

export interface UserAccessCapabilitiesDto {
  isTeacher: boolean;
  hasAll: boolean;
  canAccessAcademic: boolean;
  canAccessHealth: boolean;
  canAccessBus: boolean;
  canAccessWorkspace: boolean;
  canAccessAtRisk: boolean;
  canAccessCommendations: boolean;
  canAccessSupport: boolean;
  restrictsClasses: boolean;
  scopes: string[];
  homerooms: string[];
}

export interface MetricCardDto {
  label: string;
  value: string;
  delta: string;
}

export interface DashboardSummaryDto {
  metrics: MetricCardDto[];
}

export interface AdminUserDto {
  id: string;
  username: string;
  email?: string | null;
  fullName: string;
  role: string;
  createdAt: string;
  lastLoginAt?: string | null;
  accessScopes: string[];
  homerooms: string[];
}

export interface CreateAdminUserDto {
  username: string;
  password: string;
  fullName: string;
  email?: string | null;
  role: string;
  accessScopes: string[];
  homerooms: string[];
}

export interface UpdateAdminUserDto {
  fullName: string;
  email?: string | null;
  role: string;
  newPassword?: string | null;
  accessScopes: string[];
  homerooms: string[];
}
