import { useTranslation } from 'react-i18next';

import { ConfigurationPageLayout } from '@/components/layout/ConfigurationPageLayout';
import { useSetBreadcrumbs } from '@/hooks/useSetBreadcrumbs';
import { useUrlState } from '@/hooks/useUrlState';

import { ApiTokensPage } from './ApiTokensPage';

export function AccountSettingsPage() {
  const { t } = useTranslation('settings');
  useSetBreadcrumbs([{ label: t('account.title') }]);

  const [selectedTab, setSelectedTab] = useUrlState('tab', 'api-tokens');

  const sections = [
    {
      id: 'account',
      label: t('account.menuTitle'),
      items: [{ id: 'api-tokens', label: t('menu.apiTokens'), content: <ApiTokensPage /> }],
    },
  ];

  return (
    <ConfigurationPageLayout
      mainHeader={<h1>{t('account.title')}</h1>}
      configHeader={<h1>{t('account.title')}</h1>}
      sections={sections}
      isConfigOpen
      hideCloseButton
      hideConfigButton
      selectedMenuId={selectedTab}
      onSelectedMenuIdChange={setSelectedTab}
    >
      {null}
    </ConfigurationPageLayout>
  );
}
