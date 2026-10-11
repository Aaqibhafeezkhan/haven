import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { authApi, GenerateApiTokenInput, GetApiTokensParams } from '@/api/auth';

const API_TOKENS_KEY = 'apiTokens';

export function useApiTokens(params?: GetApiTokensParams) {
  return useQuery({
    queryKey: [API_TOKENS_KEY, params],
    queryFn: () => authApi.getApiTokens(params),
  });
}

export function useGenerateApiToken() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: GenerateApiTokenInput) => authApi.generateApiToken(input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [API_TOKENS_KEY] });
    },
  });
}

export function useRegenerateApiToken() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => authApi.regenerateApiToken(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [API_TOKENS_KEY] });
    },
  });
}

export function useRevokeApiToken() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => authApi.revokeApiToken(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [API_TOKENS_KEY] });
    },
  });
}
