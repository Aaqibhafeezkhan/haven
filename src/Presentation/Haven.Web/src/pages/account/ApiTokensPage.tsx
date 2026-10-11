import { KeyRound } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Row, Stack } from '@/components/layout';
import { Banner } from '@/components/ui/Banner';
import { Button } from '@/components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/Card';
import { CodeBlock } from '@/components/ui/CodeBlock';
import { Input } from '@/components/ui/Input';
import { Label } from '@/components/ui/Label';
import { Modal } from '@/components/ui/Modal';
import { PermissionSelect } from '@/components/ui/PermissionSelect';
import { SelectInput } from '@/components/ui/SelectInput';
import { Spinner } from '@/components/ui/Spinner';
import { useGenerateApiToken } from '@/hooks/useApiTokens';
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
          <Label variant="muted">{t('apiTokens.description')}</Label>
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
