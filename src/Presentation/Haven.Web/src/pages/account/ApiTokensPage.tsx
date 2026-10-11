import { KeyRound, RefreshCw } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ApiTokenDto } from '@/api/auth';
import { Row, Spacer, Stack } from '@/components/layout';
import { Badge } from '@/components/ui/Badge';
import { Banner } from '@/components/ui/Banner';
import { Button } from '@/components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/Card';
import { CodeBlock } from '@/components/ui/CodeBlock';
import { CodeSpan } from '@/components/ui/CodeSpan';
import { Input } from '@/components/ui/Input';
import { Label } from '@/components/ui/Label';
import { Modal } from '@/components/ui/Modal';
import { Pagination } from '@/components/ui/Pagination';
import { PermissionSelect } from '@/components/ui/PermissionSelect';
import { SelectInput } from '@/components/ui/SelectInput';
import { Spinner } from '@/components/ui/Spinner';
import { Tooltip } from '@/components/ui/Tooltip';
import { useApiTokens, useGenerateApiToken, useRegenerateApiToken } from '@/hooks/useApiTokens';
import { useFormatDate } from '@/hooks/useFormatDate';
import { secondsToTimeSpan } from '@/lib/timespan';

const DAY = 86400;
const EXPIRATION_SECONDS: Record<string, number | undefined> = {
  never: undefined,
  '7d': 7 * DAY,
  '30d': 30 * DAY,
  '90d': 90 * DAY,
  '365d': 365 * DAY,
};

export function ApiTokensPage() {
  const { t } = useTranslation('settings');
  const formatDate = useFormatDate();
  const [pageNumber, setPageNumber] = useState(1);
  const { data: tokens, isLoading } = useApiTokens({ pageNumber, pageSize: 10 });
  const { mutateAsync: regenerate, isPending: isRegenerating } = useRegenerateApiToken();
  const [tokenToRegenerate, setTokenToRegenerate] = useState<ApiTokenDto | null>(null);
  const [regenerateError, setRegenerateError] = useState<string | undefined>();
  const { mutateAsync: generate, isPending } = useGenerateApiToken();

  const [isOpen, setIsOpen] = useState(false);
  const [name, setName] = useState('');
  const [scopes, setScopes] = useState<string[]>([]);
  const [expiration, setExpiration] = useState('30d');
  const [formError, setFormError] = useState<string | undefined>();
  const [generatedToken, setGeneratedToken] = useState<string | null>(null);

  const expirationOptions = Object.keys(EXPIRATION_SECONDS).map(value => ({
    value,
    label: t(`apiTokens.expirations.${value}` as 'apiTokens.expirations.never'),
  }));

  const reset = () => {
    setIsOpen(false);
    setName('');
    setScopes([]);
    setExpiration('30d');
    setFormError(undefined);
  };

  const handleGenerate = async () => {
    setFormError(undefined);
    try {
      const seconds = EXPIRATION_SECONDS[expiration];
      const result = await generate({
        name: name.trim(),
        scopes,
        expiration: seconds === undefined ? undefined : secondsToTimeSpan(seconds),
      });
      setGeneratedToken(result.plainText);
      reset();
    } catch (err: unknown) {
      setFormError(err instanceof Error ? err.message : t('apiTokens.errors.generateFailed'));
    }
  };

  const closeRegenerate = () => {
    setTokenToRegenerate(null);
    setRegenerateError(undefined);
  };

  const handleRegenerate = async () => {
    if (!tokenToRegenerate) return;
    setRegenerateError(undefined);
    try {
      const result = await regenerate(tokenToRegenerate.id);
      setGeneratedToken(result.plainText);
      closeRegenerate();
    } catch (err: unknown) {
      setRegenerateError(
        err instanceof Error ? err.message : t('apiTokens.errors.regenerateFailed')
      );
    }
  };

  return (
    <>
      <Card>
        <CardHeader>
          <Row justify="space-between">
            <CardTitle>{t('apiTokens.title')}</CardTitle>
            <Button icon={<KeyRound />} onClick={() => setIsOpen(true)}>
              {t('apiTokens.generate')}
            </Button>
          </Row>
        </CardHeader>
        <CardContent>
          <Stack gap="3">
            <Label variant="muted">{t('apiTokens.description')}</Label>
            {isLoading ? (
              <Row justify="center">
                <Spinner />
              </Row>
            ) : !tokens?.items.length ? (
              <Label variant="muted">{t('apiTokens.empty')}</Label>
            ) : (
              <Stack gap="2">
                {tokens.items.map(token => {
                  const status = token.revokedAt
                    ? 'revoked'
                    : token.expiresAt && new Date(token.expiresAt) <= new Date()
                      ? 'expired'
                      : 'active';
                  return (
                    <Card key={token.id} padding="var(--space-3)">
                      <Row>
                        <Stack gap="1">
                          <Row gap="1">
                            <Label size="md" variant="primary" style={{ fontWeight: 'bold' }}>
                              {token.displayName}
                            </Label>
                            <CodeSpan>{token.prefix}</CodeSpan>
                          </Row>
                          <Label size="sm" variant="secondary">
                            {[
                              t('apiTokens.createdOn', { date: formatDate(token.createdAt) }),
                              token.expiresAt
                                ? t('apiTokens.expiresOn', { date: formatDate(token.expiresAt) })
                                : t('apiTokens.neverExpires'),
                              token.lastUsedAt
                                ? t('apiTokens.lastUsed', { date: formatDate(token.lastUsedAt) })
                                : t('apiTokens.neverUsed'),
                            ].join(' · ')}
                          </Label>
                        </Stack>
                        <Spacer expand />
                        <Tooltip content={token.scopes.join(', ')} direction="left">
                          <Badge>{t('apiTokens.scopeCount', { count: token.scopes.length })}</Badge>
                        </Tooltip>
                        {status !== 'revoked' && (
                          <Tooltip content={t('apiTokens.regenerate')} direction="left">
                            <Button
                              size="md"
                              variant="text"
                              icon={<RefreshCw />}
                              onClick={() => setTokenToRegenerate(token)}
                            />
                          </Tooltip>
                        )}
                        <Badge
                          variant={
                            status === 'active'
                              ? 'success'
                              : status === 'expired'
                                ? 'warning'
                                : 'danger'
                          }
                        >
                          {t(`apiTokens.statuses.${status}`)}
                        </Badge>
                      </Row>
                    </Card>
                  );
                })}
                <Pagination
                  pageNumber={tokens.pageNumber}
                  totalPages={tokens.totalPages}
                  hasPreviousPage={tokens.hasPreviousPage}
                  hasNextPage={tokens.hasNextPage}
                  onPreviousPage={() => setPageNumber(p => Math.max(1, p - 1))}
                  onNextPage={() => setPageNumber(p => p + 1)}
                />
              </Stack>
            )}
          </Stack>
        </CardContent>
      </Card>

      <Modal
        isOpen={isOpen}
        onClose={reset}
        title={t('apiTokens.generateModal.title')}
        error={formError}
        footer={
          <Row justify="flex-end">
            <Button variant="secondary" onClick={reset}>
              {t('apiTokens.generateModal.cancel')}
            </Button>
            <Button onClick={handleGenerate} disabled={isPending || !name.trim() || !scopes.length}>
              {isPending ? <Spinner /> : t('apiTokens.generateModal.submit')}
            </Button>
          </Row>
        }
      >
        <Stack gap="4">
          <Input
            label={t('apiTokens.generateModal.nameLabel')}
            value={name}
            onChange={e => setName(e.target.value)}
            placeholder={t('apiTokens.generateModal.namePlaceholder')}
            maxLength={100}
            required
          />
          <PermissionSelect
            label={t('apiTokens.generateModal.scopes')}
            placeholder={t('apiTokens.generateModal.scopesPlaceholder')}
            value={scopes}
            onChange={setScopes}
            onlyAttributed
            required
          />
          <SelectInput
            label={t('apiTokens.generateModal.expiration')}
            options={expirationOptions}
            value={expiration}
            onChange={setExpiration}
          />
        </Stack>
      </Modal>

      <Modal
        isOpen={tokenToRegenerate !== null}
        onClose={closeRegenerate}
        title={t('apiTokens.regenerateModal.title')}
        error={regenerateError}
        footer={
          <Row justify="flex-end">
            <Button variant="secondary" onClick={closeRegenerate}>
              {t('apiTokens.regenerateModal.cancel')}
            </Button>
            <Button onClick={handleRegenerate} disabled={isRegenerating}>
              {isRegenerating ? <Spinner /> : t('apiTokens.regenerateModal.submit')}
            </Button>
          </Row>
        }
      >
        <Banner
          variant="warning"
          description={t('apiTokens.regenerateModal.warning', {
            name: tokenToRegenerate?.displayName ?? '',
          })}
        />
      </Modal>

      <Modal
        isOpen={generatedToken !== null}
        onClose={() => setGeneratedToken(null)}
        title={t('apiTokens.createdModal.title')}
        closeOnBackdropClick={false}
        footer={
          <Row justify="flex-end">
            <Button onClick={() => setGeneratedToken(null)}>
              {t('apiTokens.createdModal.done')}
            </Button>
          </Row>
        }
      >
        <Stack gap="4">
          <Banner variant="warning" description={t('apiTokens.createdModal.warning')} />
          <CodeBlock code={generatedToken ?? ''} copyable />
        </Stack>
      </Modal>
    </>
  );
}
