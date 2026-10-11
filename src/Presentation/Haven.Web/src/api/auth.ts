import { apiClient } from './client';
import type { PagedResult } from './types';

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
}

export interface MeResponse {
  id: string;
  name: string;
  email: string;
  requirePasswordChange: boolean;
  isAdmin: boolean;
  permissions: string[];
}

export interface LoginInput {
  email: string;
  password: string;
}

export interface SetPasswordInput {
  newPassword: string;
  confirmPassword: string;
}

export interface AcceptInviteInput {
  token: string;
  name: string;
  password: string;
  confirmPassword: string;
}

export interface GenerateApiTokenInput {
  name: string;
  scopes: string[];
  /** .NET TimeSpan string; omit for a token that never expires. */
  expiration?: string;
}

export interface GenerateApiTokenResult {
  plainText: string;
}

export interface ApiTokenDto {
  id: string;
  displayName: string;
  prefix: string;
  scopes: string[];
  createdAt: string;
  expiresAt: string | null;
  lastUsedAt: string | null;
  revokedAt: string | null;
}

export interface GetApiTokensParams {
  pageNumber?: number;
  pageSize?: number;
}

export const authApi = {
  login: (input: LoginInput) => apiClient.post<AuthResponse>('/auth/login', input),
  me: () => apiClient.get<MeResponse>('/auth/me'),
  logout: () => apiClient.post<void>('/auth/logout', {}),
  setPassword: (input: SetPasswordInput) => apiClient.post<void>('/auth/set-password', input),
  acceptInvite: (input: AcceptInviteInput) =>
    apiClient.post<AuthResponse>('/auth/accept-invite', input),
  getApiTokens: (params?: GetApiTokensParams) =>
    apiClient.get<PagedResult<ApiTokenDto>>('/auth/api-tokens', params),
  generateApiToken: (input: GenerateApiTokenInput) =>
    apiClient.post<GenerateApiTokenResult>('/auth/api-tokens', input),
};
