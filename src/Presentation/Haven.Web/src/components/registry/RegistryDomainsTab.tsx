import { Globe, SearchX } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';

import { serviceRegistryApi } from '@/api/serviceRegistry';
import type { ServiceRegistryDomainSortBy, TlsMode } from '@/api/types';
import {
  Row,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/layout';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Card, CardContent, CardHeader } from '@/components/ui/Card';
import { CopyButton } from '@/components/ui/CopyButton';
import { ErrorAlert } from '@/components/ui/ErrorAlert';
import { HealthIndicator } from '@/components/ui/HealthIndicator';
import { Pagination } from '@/components/ui/Pagination';
import { SearchInput } from '@/components/ui/SearchInput';
import { Spinner } from '@/components/ui/Spinner';
import { useRefreshableQuery } from '@/hooks/useRefreshableQuery';
import { useSetUrlParams, useUrlState } from '@/hooks/useUrlState';
import styles from '@/styles/components/registry/Registry.module.css';

import { canOpenService, domainUrl, entryContext } from './registryUtils';

const PAGE_SIZE = 20;

interface Props {
  refreshTick: number;
}

const tlsVariant = (mode: TlsMode) => (mode === 'None' ? 'default' : 'success');

export function RegistryDomainsTab({ refreshTick }: Props) {
  const { t } = useTranslation('serviceRegistry');
  const navigate = useNavigate();

  const [search, setSearch] = useUrlState('dq', '');
  const [sortBy] = useUrlState('dsort', 'hostname');
  const [dir] = useUrlState('ddir', 'asc');
  const sortDescending = dir === 'desc';
  const setUrlParams = useSetUrlParams();

  const [searchInput, setSearchInput] = useState(search);
  const [page, setPage] = useState(1);

  useEffect(() => {
    const id = setTimeout(() => {
      if (searchInput.trim() !== search) {
        setSearch(searchInput.trim());
        setPage(1);
      }
    }, 300);
    return () => clearTimeout(id);
  }, [searchInput, search, setSearch]);

  const { data, loading, error } = useRefreshableQuery(
    () =>
      serviceRegistryApi.getDomains({
        pageNumber: page,
        pageSize: PAGE_SIZE,
        search: search || undefined,
        sortBy: sortBy as ServiceRegistryDomainSortBy,
        sortDescending,
      }),
    JSON.stringify([page, search, sortBy, sortDescending]),
    refreshTick,
    t('error')
  );

  const handleSort = (key: string) => {
    setUrlParams(
      sortBy === key ? { ddir: sortDescending ? 'asc' : 'desc' } : { dsort: key, ddir: 'asc' }
    );
    setPage(1);
  };
  const sortProps = { sortBy, sortDescending, onSort: handleSort };

  if (error && !data) return <ErrorAlert message={error} variant="block" />;
  if (loading && !data) {
    return (
      <div className={styles.spinner}>
        <Spinner />
        <p>{t('loading')}</p>
      </div>
    );
  }

  const items = data?.items ?? [];

  return (
    <>
      {error && <ErrorAlert message={error} variant="block" />}
      <Card>
        <CardHeader className={styles.cardHeader}>
          <Row align="center" gap="3">
            <h2 className={styles.sectionTitle}>{t('domainsTab.title')}</h2>
            {data && <Badge>{data.totalCount}</Badge>}
          </Row>
          <SearchInput
            wrapperClassName={styles.search}
            placeholder={t('domainsTab.searchPlaceholder')}
            aria-label={t('domainsTab.searchPlaceholder')}
            value={searchInput}
            onChange={e => setSearchInput(e.target.value)}
          />
        </CardHeader>
        <CardContent className={`${styles.tableContent} ${loading ? styles.reloading : ''}`}>
          {!items.length ? (
            <div className={styles.emptyState}>
              {search ? <SearchX size={28} /> : <Globe size={28} />}
              <p>{search ? t('emptySearch', { search }) : t('domainsTab.empty')}</p>
              {search && (
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => {
                    setSearch('');
                    setSearchInput('');
                  }}
                >
                  {t('search.clear')}
                </Button>
              )}
            </div>
          ) : (
            <Table hoverable={false} striped={false}>
              <TableHead className={styles.thead}>
                <TableRow isHeader>
                  <TableHeader sortKey="hostname" {...sortProps}>
                    {t('domainsTab.hostname')}
                  </TableHeader>
                  <TableHeader sortKey="service" {...sortProps}>
                    {t('domainsTab.service')}
                  </TableHeader>
                  <TableHeader>{t('table.status')}</TableHeader>
                  <TableHeader sortKey="tls" {...sortProps}>
                    {t('domainsTab.tls')}
                  </TableHeader>
                  <TableHeader>{t('domainsTab.port')}</TableHeader>
                </TableRow>
              </TableHead>
              <TableBody className={styles.tbody}>
                {items.map(d => {
                  const openable = canOpenService(d.serviceId);
                  const owner = d.serviceName ?? d.sidecarName ?? d.containerName ?? t('none');
                  const context = d.sidecarName
                    ? t('sidecarLabel', { name: d.sidecarName })
                    : entryContext(d);
                  return (
                    <TableRow
                      key={d.id}
                      className={styles.row}
                      onRowClick={openable ? () => navigate(`/services/${d.serviceId}`) : undefined}
                    >
                      <TableCell>
                        <span className={styles.copyable}>
                          <a
                            href={domainUrl(d)}
                            target="_blank"
                            rel="noreferrer"
                            className={styles.domainLink}
                            title={t('domains.open', { hostname: d.hostname })}
                            onClick={e => e.stopPropagation()}
                          >
                            <Globe size={13} />
                            <span>{d.hostname}</span>
                          </a>
                          <CopyButton
                            value={d.hostname}
                            label={t('copy.hostname')}
                            className={styles.copyButton}
                          />
                        </span>
                      </TableCell>
                      <TableCell>
                        <div className={styles.nameCell}>
                          <span className={styles.containerName}>{owner}</span>
                          {context && <span className={styles.context}>{context}</span>}
                        </div>
                      </TableCell>
                      <TableCell nowrap>
                        <HealthIndicator showLabel health={d.status} />
                      </TableCell>
                      <TableCell nowrap>
                        <Row gap="2" align="center">
                          <Badge variant={tlsVariant(d.tlsMode)}>
                            {t(`domainsTab.tlsModes.${d.tlsMode}`)}
                          </Badge>
                          {d.tlsMode === 'Custom' && !d.hasCertificate && (
                            <Badge variant="warning">{t('domainsTab.noCertificate')}</Badge>
                          )}
                          {d.certificateName && (
                            <span className={styles.muted}>{d.certificateName}</span>
                          )}
                        </Row>
                      </TableCell>
                      <TableCell variant="mono">{d.containerPort}</TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {data && (
        <Pagination
          pageNumber={data.pageNumber}
          totalPages={data.totalPages}
          hasPreviousPage={data.hasPreviousPage}
          hasNextPage={data.hasNextPage}
          onPreviousPage={() => setPage(p => Math.max(1, p - 1))}
          onNextPage={() => setPage(p => p + 1)}
        />
      )}
    </>
  );
}
