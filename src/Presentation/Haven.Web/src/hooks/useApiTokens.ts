import { useMutation } from '@tanstack/react-query';

import { authApi, GenerateApiTokenInput } from '@/api/auth';

export function useGenerateApiToken() {
  return useMutation({
    mutationFn: (input: GenerateApiTokenInput) => authApi.generateApiToken(input),
  });
}
