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
  jobTitle?: string | null;
}

export interface CurrentUserDto {
  role: string;
  username: string;
  fullName: string;
  userId: string;
  jobTitle?: string | null;
}

export interface MetricCardDto {
  label: string;
  value: string;
  delta: string;
}

export interface DashboardSummaryDto {
  metrics: MetricCardDto[];
}
