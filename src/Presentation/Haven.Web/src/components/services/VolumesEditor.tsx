import { FolderOpen, HardDrive, Pencil, Plus, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useSort } from '@/hooks/useSort';
import styles from '@/styles/components/services/VolumesEditor.module.css';

import {
  AddVolumeInput,
  ServiceVolumeDto,
  VolumeSortBy,
  VolumeType,
} from '../../api/types/volume.types';
import { volumesApi } from '../../api/volumes';
import { Row, Spacer } from '../layout';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../layout/Table';
import { Badge } from '../ui/Badge';
import { Banner } from '../ui/Banner';
import { Button } from '../ui/Button';
import { ErrorAlert } from '../ui/ErrorAlert';
import { Label } from '../ui/Label';
import { Modal } from '../ui/Modal';
import { Spinner } from '../ui/Spinner';
import { ManagedVolumeFilesModal } from './ManagedVolumeFilesModal';
import { VolumeFormModal, volumeTypeKey } from './VolumeFormModal';

interface VolumesEditorProps {
  projectId: string;
  environmentId: string;
  serviceId: string;
  readOnly?: boolean;
}

const badgeVariant = (type: VolumeType) =>
  type === 'Managed' ? 'primary' : type === 'HostPath' ? 'warning' : 'default';

export function VolumesEditor({
  projectId,
  environmentId,
  serviceId,
  readOnly = false,
}: VolumesEditorProps) {
  const { t } = useTranslation('services');

  const [volumes, setVolumes] = useState<ServiceVolumeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const { sortBy, sortDescending, onSort } = useSort<VolumeSortBy>('name');

  const [isAddOpen, setIsAddOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ServiceVolumeDto | null>(null);

  const [deleteTarget, setDeleteTarget] = useState<ServiceVolumeDto | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const [filesVolume, setFilesVolume] = useState<ServiceVolumeDto | null>(null);

  const loadVolumes = useCallback(async () => {
    try {
      const result = await volumesApi.list(projectId, environmentId, serviceId, {
        sortBy,
        sortDescending,
      });
      setVolumes(result ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('error'));
    } finally {
      setLoading(false);
    }
  }, [projectId, environmentId, serviceId, sortBy, sortDescending, t]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- initial data fetch on mount
    void loadVolumes();
  }, [loadVolumes]);

  const handleCreate = async (input: AddVolumeInput) => {
    await volumesApi.add(projectId, environmentId, serviceId, input);
    setIsAddOpen(false);
    await loadVolumes();
  };

  const handleUpdate = async (input: AddVolumeInput) => {
    if (!editTarget) return;
    await volumesApi.update(projectId, environmentId, serviceId, editTarget.id, {
      name: input.name,
      target: input.target,
      source: input.source,
      readOnly: input.readOnly,
      backupEnabled: input.backupEnabled,
    });
    setEditTarget(null);
    await loadVolumes();
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      setIsDeleting(true);
      await volumesApi.delete(projectId, environmentId, serviceId, deleteTarget.id);
      setDeleteTarget(null);
      await loadVolumes();
    } catch (err) {
      setError(err instanceof Error ? err.message : t('error'));
    } finally {
      setIsDeleting(false);
    }
  };

  if (loading) {
    return (
      <div className={styles.spinnerWrap}>
        <Spinner />
      </div>
    );
  }

  return (
    <div className={styles.container}>
      {error && <ErrorAlert message={error} variant="block" />}
      {readOnly && <Banner variant="info" description={t('repositoryManagedVolumes')} />}

      <Row align="center" gap="2">
        <Label variant="primary" size="md" weight="semibold">
          {t('volumes.title')}
        </Label>
        {volumes.length > 0 && <Badge>{volumes.length}</Badge>}
        <Spacer expand direction="horizontal" />
        {!readOnly && (
          <Button
            variant="secondary"
            size="sm"
            icon={<Plus size={14} />}
            onClick={() => setIsAddOpen(true)}
          >
            {t('volumes.add')}
          </Button>
        )}
      </Row>

      {volumes.length === 0 ? (
        <div className={styles.emptyState}>
          <HardDrive size={28} className={styles.emptyIcon} />
          <Label variant="secondary" size="sm">
            {t('volumes.empty')}
          </Label>
          {!readOnly && (
            <Button
              variant="secondary"
              size="sm"
              icon={<Plus size={14} />}
              onClick={() => setIsAddOpen(true)}
            >
              {t('volumes.addFirst')}
            </Button>
          )}
        </div>
      ) : (
        <div className={styles.tableWrap}>
          <Table>
            <TableHead>
              <TableRow isHeader hasActionsColumn>
                <TableHeader
                  sortKey="name"
                  sortBy={sortBy}
                  sortDescending={sortDescending}
                  onSort={onSort}
                >
                  {t('volumes.columns.name')}
                </TableHeader>
                <TableHeader
                  sortKey="type"
                  sortBy={sortBy}
                  sortDescending={sortDescending}
                  onSort={onSort}
                >
                  {t('volumes.columns.type')}
                </TableHeader>
                <TableHeader>{t('volumes.columns.source')}</TableHeader>
                <TableHeader
                  sortKey="target"
                  sortBy={sortBy}
                  sortDescending={sortDescending}
                  onSort={onSort}
                >
                  {t('volumes.columns.target')}
                </TableHeader>
                <TableHeader>{t('volumes.columns.options')}</TableHeader>
              </TableRow>
            </TableHead>
            <TableBody>
              {volumes.map(volume => {
                const isManaged = volume.type === 'Managed';
                return (
                  <TableRow
                    key={volume.id}
                    onRowClick={readOnly ? undefined : () => setEditTarget(volume)}
                    actions={
                      <>
                        {isManaged && (
                          <button
                            className={styles.iconBtn}
                            onClick={e => {
                              e.stopPropagation();
                              setFilesVolume(volume);
                            }}
                            title={t('volumes.manageFiles')}
                          >
                            <FolderOpen size={14} />
                          </button>
                        )}
                        {!readOnly && (
                          <>
                            <button
                              className={styles.iconBtn}
                              onClick={e => {
                                e.stopPropagation();
                                setEditTarget(volume);
                              }}
                              title={t('volumes.edit')}
                            >
                              <Pencil size={14} />
                            </button>
                            <button
                              className={`${styles.iconBtn} ${styles.iconBtnDanger}`}
                              onClick={e => {
                                e.stopPropagation();
                                setDeleteTarget(volume);
                              }}
                              title={t('volumes.delete')}
                            >
                              <Trash2 size={14} />
                            </button>
                          </>
                        )}
                      </>
                    }
                  >
                    <TableCell variant="highlight">{volume.name}</TableCell>
                    <TableCell>
                      <Badge variant={badgeVariant(volume.type)}>
                        {t(`volumes.types.${volumeTypeKey[volume.type]}`)}
                      </Badge>
                    </TableCell>
                    <TableCell variant={isManaged ? 'muted' : 'mono'}>
                      {isManaged ? t('volumes.managedSource') : volume.source}
                    </TableCell>
                    <TableCell variant="mono">{volume.target}</TableCell>
                    <TableCell>
                      <div className={styles.tags}>
                        {volume.readOnly && <Badge>{t('volumes.readOnlyShort')}</Badge>}
                        {isManaged && volume.backupEnabled && (
                          <Badge variant="success">{t('volumes.backupShort')}</Badge>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      )}

      {isAddOpen && <VolumeFormModal onClose={() => setIsAddOpen(false)} onSubmit={handleCreate} />}

      {editTarget && (
        <VolumeFormModal
          key={editTarget.id}
          volume={editTarget}
          onClose={() => setEditTarget(null)}
          onSubmit={handleUpdate}
        />
      )}

      <Modal
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        title={t('volumes.deleteTitle')}
        size="sm"
        footer={
          <Row gap="2" justify="flex-end" full>
            <Button variant="ghost" onClick={() => setDeleteTarget(null)} disabled={isDeleting}>
              {t('volumes.cancel')}
            </Button>
            <Button variant="danger" onClick={handleDelete} isLoading={isDeleting}>
              {t('volumes.delete')}
            </Button>
          </Row>
        }
      >
        <Label variant="secondary" size="sm">
          {t('volumes.deleteConfirm', { name: deleteTarget?.name })}
        </Label>
      </Modal>

      {filesVolume && (
        <ManagedVolumeFilesModal
          projectId={projectId}
          environmentId={environmentId}
          serviceId={serviceId}
          volume={filesVolume}
          isOpen={!!filesVolume}
          onClose={() => setFilesVolume(null)}
        />
      )}
    </div>
  );
}
