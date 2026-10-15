import { ArrowRight, ChevronRight, SearchX, ServerOff } from 'lucide-react';
import { Fragment, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';

import { environmentsApi } from '@/api/environments';
import { projectsApi } from '@/api/projects';
import { serviceRegistryApi } from '@/api/serviceRegistry';
import type {
  EnvironmentDto,
  ExposureMode,
  ProjectDto,
  RegistryStatusFilter,
  ServiceRegistrySortBy,
  ServiceType,
} from '@/api/types';
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
import { ServiceExposureChip } from '@/components/ui/chips/serviceExposureChip';
import { ServiceTypeChip } from '@/components/ui/chips/serviceTypeChip';
import { ErrorAlert } from '@/components/ui/ErrorAlert';
import { FilterPillGroup } from '@/components/ui/FilterPillGroup';
import { HealthIndicator } from '@/components/ui/HealthIndicator';
import { Pagination } from '@/components/ui/Pagination';
import { SearchInput } from '@/components/ui/SearchInput';
import { SelectInput } from '@/components/ui/SelectInput';
import { Spinner } from '@/components/ui/Spinner';
import { useRefreshableQuery } from '@/hooks/useRefreshableQuery';
import { useSetUrlParams, useUrlState } from '@/hooks/useUrlState';
import styles from '@/styles/components/registry/Registry.module.css';

import { AddressLinks, DomainLinks, EntryName } from './RegistryCells';
import { RegistryEntryDetails } from './RegistryEntryDetails';
import { canOpenService, formatUptime } from './registryUtils';

const PAGE_SIZE = 20;
const COLUMN_COUNT = 8;

interface Props {
  refreshTick: number;
}

export function RegistryEntriesTab({ refreshTick }: Props) {
  const { t } = useTranslation('serviceRegistry');
  const navigate = useNavigate();

  const [search, setSearch] = useUrlState('q', '');
  const [status, setStatus] = useUrlState('status', '');
  const [projectId] = useUrlState('project', '');
  const [environmentId, setEnvironmentId] = useUrlState('env', '');
  const [exposure, setExposure] = useUrlState('exposure', '');
  const [type, setType] = useUrlState('type', '');
  const [sortBy] = useUrlState('sort', 'name');
  const [dir] = useUrlState('dir', 'asc');
  const sortDescending = dir === 'desc';
  const setUrlParams = useSetUrlParams();

  const [searchInput, setSearchInput] = useState(search);
  const [page, setPage] = useState(1);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  const [projects, setProjects] = useState<ProjectDto[]>([]);
  const [environments, setEnvironments] = useState<EnvironmentDto[]>([]);

  useEffect(() => {
    const id = setTimeout(() => {
      if (searchInput.trim() !== search) {
        setSearch(searchInput.trim());
        setPage(1);
      }
    }, 300);
    return () => clearTimeout(id);
  }, [searchInput, search, setSearch]);

  useEffect(() => {
    let cancelled = false;
    projectsApi
      .getAll({ pageNumber: 1, pageSize: 100 })
      .then(r => !cancelled && setProjects(r.items))
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    if (!projectId) return;
    environmentsApi
      .getByProjectId(projectId)
      .then(r => !cancelled && setEnvironments(r))
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [projectId]);

  const queryKey = JSON.stringify([
    page,
    search,
    status,
    projectId,
    environmentId,
    exposure,
    type,
    sortBy,
    sortDescending,
  ]);

  const { data, loading, error } = useRefreshableQuery(
    () =>
      serviceRegistryApi.getAll({
        pageNumber: page,
        pageSize: PAGE_SIZE,
        search: search || undefined,
        status: (status || undefined) as RegistryStatusFilter | undefined,
        projectId: projectId || undefined,
        environmentId: environmentId || undefined,
        exposure: (exposure || undefined) as ExposureMode | undefined,
        type: (type || undefined) as ServiceType | undefined,
        sortBy: sortBy as ServiceRegistrySortBy,
        sortDescending,
      }),
    queryKey,
    refreshTick,
    t('error')
  );

  const resetPage = () => setPage(1);
  const filtered = (setter: (v: string) => void) => (value: string) => {
    setter(value);
    resetPage();
  };

  const handleSort = (key: string) => {
    setUrlParams(
      sortBy === key ? { dir: sortDescending ? 'asc' : 'desc' } : { sort: key, dir: 'asc' }
    );
    resetPage();
  };

  const hasFilters = !!(status || projectId || environmentId || exposure || type || search);
  const clearAll = () => {
    setUrlParams({ status: '', project: '', env: '', exposure: '', type: '', q: '' });
    setSearchInput('');
    resetPage();
  };

  const toggleExpanded = (id: string) =>
    setExpanded(prev => {
      const next = new Set(prev);
      if (!next.delete(id)) next.add(id);
      return next;
    });

  const statusOptions = useMemo(
    () => [
      { value: '', label: t('filters.all') },
      { value: 'Running', label: t('filters.running') },
      { value: 'Stopped', label: t('filters.stopped') },
      { value: 'Starting', label: t('filters.starting') },
      { value: 'Unknown', label: t('filters.unknown') },
    ],
    [t]
  );
  const projectOptions = useMemo(
    () => [
      { value: '', label: t('filters.allProjects') },
      ...projects.map(p => ({ value: p.id, label: p.name })),
    ],
    [projects, t]
  );
  const environmentOptions = useMemo(
    () => [
      { value: '', label: t('filters.allEnvironments') },
      ...environments
        .filter(e => e.projectId === projectId)
        .map(e => ({ value: e.id, label: e.name })),
    ],
    [environments, projectId, t]
  );
  const exposureOptions = useMemo(
    () => [
      { value: '', label: t('filters.anyExposure') },
      ...(['None', 'Internal', 'External', 'Custom'] as const).map(v => ({
        value: v,
        label: t(`filters.exposure.${v}`),
      })),
    ],
    [t]
  );
  const typeOptions = useMemo(
    () => [
      { value: '', label: t('filters.anyType') },
      ...(['DockerImage', 'Dockerfile'] as const).map(v => ({
        value: v,
        label: t(`filters.types.${v}`),
      })),
    ],
    [t]
  );

  const sortProps = { sortBy, sortDescending, onSort: handleSort };
  const items = data?.items ?? [];

  if (error && !data) return <ErrorAlert message={error} variant="block" />;
  if (loading && !data) {
    return (
      <div className={styles.spinner}>
        <Spinner />
        <p>{t('loading')}</p>
      </div>
    );
  }

  return (
    <>
      {error && <ErrorAlert message={error} variant="block" />}
      <Card>
        <CardHeader className={styles.cardHeader}>
          <Row align="center" gap="3">
            <h2 className={styles.sectionTitle}>{t('sectionTitle')}</h2>
            {data && <Badge>{data.totalCount}</Badge>}
          </Row>
          <SearchInput
            wrapperClassName={styles.search}
            placeholder={t('search.placeholder')}
            aria-label={t('search.placeholder')}
            value={searchInput}
            onChange={e => setSearchInput(e.target.value)}
          />
        </CardHeader>

        <div className={styles.filterBar}>
          <FilterPillGroup options={statusOptions} value={status} onChange={filtered(setStatus)} />
          <div className={styles.selects}>
            <SelectInput
              options={projectOptions}
              value={projectId}
              onChange={value => {
                setUrlParams({ project: value, env: '' });
                resetPage();
              }}
            />
            {projectId && (
              <SelectInput
                options={environmentOptions}
                value={environmentId}
                onChange={filtered(setEnvironmentId)}
              />
            )}
            <SelectInput
              options={exposureOptions}
              value={exposure}
              onChange={filtered(setExposure)}
            />
            <SelectInput options={typeOptions} value={type} onChange={filtered(setType)} />
            {hasFilters && (
              <Button variant="text" size="sm" onClick={clearAll}>
                {t('filters.clear')}
              </Button>
            )}
          </div>
        </div>

        <CardContent className={`${styles.tableContent} ${loading ? styles.reloading : ''}`}>
          {!items.length ? (
            <div className={styles.emptyState}>
              {hasFilters ? <SearchX size={28} /> : <ServerOff size={28} />}
              <p>{hasFilters ? t('emptyFiltered') : t('empty')}</p>
              {hasFilters && (
                <Button variant="secondary" size="sm" onClick={clearAll}>
                  {t('filters.clear')}
                </Button>
              )}
            </div>
          ) : (
            <Table hoverable={false} striped={false}>
              <TableHead className={styles.thead}>
                <TableRow isHeader>
                  <TableHeader sortKey="name" {...sortProps}>
                    {t('table.name')}
                  </TableHeader>
                  <TableHeader sortKey="status" {...sortProps}>
                    {t('table.status')}
                  </TableHeader>
                  <TableHeader sortKey="type" {...sortProps}>
                    {t('table.type')}
                  </TableHeader>
                  <TableHeader>{t('table.domains')}</TableHeader>
                  <TableHeader>{t('table.addresses')}</TableHeader>
                  <TableHeader sortKey="exposure" {...sortProps}>
                    {t('table.exposure')}
                  </TableHeader>
                  <TableHeader sortKey="uptime" {...sortProps}>
                    {t('table.uptime')}
                  </TableHeader>
                  <TableHeader>{null}</TableHeader>
                </TableRow>
              </TableHead>
              <TableBody className={styles.tbody}>
                {items.map(entry => {
                  const isOpen = expanded.has(entry.id);
                  const openable = canOpenService(entry.serviceId);
                  return (
                    <Fragment key={entry.id}>
                      <TableRow
                        className={`${styles.row} ${isOpen ? styles.rowOpen : ''}`}
                        onRowClick={
                          openable ? () => navigate(`/services/${entry.serviceId}`) : undefined
                        }
                      >
                        <TableCell>
                          <EntryName entry={entry} />
                        </TableCell>
                        <TableCell nowrap>
                          <HealthIndicator showLabel health={entry.status} />
                        </TableCell>
                        <TableCell>
                          <ServiceTypeChip serviceType={entry.serviceType} />
                        </TableCell>
                        <TableCell>
                          <DomainLinks entry={entry} />
                        </TableCell>
                        <TableCell>
                          <AddressLinks entry={entry} />
                        </TableCell>
                        <TableCell nowrap>
                          <ServiceExposureChip exposureMode={entry.exposureMode} />
                        </TableCell>
                        <TableCell variant="muted" nowrap>
                          {entry.startedAt ? formatUptime(entry.startedAt) : t('none')}
                        </TableCell>
                        <TableCell nowrap>
                          <Row gap="1" align="center" justify="flex-end">
                            <button
                              type="button"
                              className={`${styles.iconButton} ${isOpen ? styles.chevronOpen : ''}`}
                              aria-expanded={isOpen}
                              aria-label={isOpen ? t('details.collapse') : t('details.expand')}
                              title={isOpen ? t('details.collapse') : t('details.expand')}
                              onClick={e => {
                                e.stopPropagation();
                                toggleExpanded(entry.id);
                              }}
                            >
                              <ChevronRight size={16} />
                            </button>
                            {openable && (
                              <Button
                                variant="text"
                                size="xs"
                                icon={<ArrowRight size={14} />}
                                aria-label={t('openService')}
                                onClick={e => {
                                  e.stopPropagation();
                                  navigate(`/services/${entry.serviceId}`);
                                }}
                              />
                            )}
                          </Row>
                        </TableCell>
                      </TableRow>
                      {isOpen && (
                        <tr className={styles.detailRow}>
                          <td colSpan={COLUMN_COUNT}>
                            <RegistryEntryDetails entry={entry} />
                          </td>
                        </tr>
                      )}
                    </Fragment>
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
