import { Plus, Save } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { AddVolumeInput, ServiceVolumeDto, VolumeType } from '../../api/types/volume.types';
import { Row, Stack } from '../layout';
import { Button } from '../ui/Button';
import { Checkbox } from '../ui/Checkbox';
import { Input } from '../ui/Input';
import { Label } from '../ui/Label';
import { Modal } from '../ui/Modal';
import { SelectInput } from '../ui/SelectInput';

export const volumeTypeKey: Record<VolumeType, 'managed' | 'named' | 'hostPath'> = {
  Managed: 'managed',
  Named: 'named',
  HostPath: 'hostPath',
};

interface VolumeFormModalProps {
  /** When provided the modal edits this volume (its type is fixed); otherwise it creates one. */
  volume?: ServiceVolumeDto;
  onClose: () => void;
  onSubmit: (input: AddVolumeInput) => Promise<void>;
}

export function VolumeFormModal({ volume, onClose, onSubmit }: VolumeFormModalProps) {
  const { t } = useTranslation('services');
  const isEdit = !!volume;

  const [form, setForm] = useState<AddVolumeInput>({
    type: volume?.type ?? 'Managed',
    name: volume?.name ?? '',
    target: volume?.target ?? '',
    source: volume?.source ?? '',
    readOnly: volume?.readOnly ?? false,
    backupEnabled: volume?.backupEnabled ?? false,
  });
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const typeOptions = [
    { value: 'Managed', label: t('volumes.types.managed') },
    { value: 'Named', label: t('volumes.types.named') },
    { value: 'HostPath', label: t('volumes.types.hostPath') },
  ];

  const needsSource = form.type === 'Named' || form.type === 'HostPath';
  const canSubmit =
    !!form.name.trim() && !!form.target.trim() && (!needsSource || !!form.source?.trim());

  const handleSubmit = async () => {
    if (!canSubmit) return;
    try {
      setIsSaving(true);
      setError(null);
      await onSubmit({
        type: form.type,
        name: form.name.trim(),
        target: form.target.trim(),
        source: needsSource ? form.source?.trim() : undefined,
        readOnly: form.readOnly,
        backupEnabled: form.type === 'Managed' && form.backupEnabled,
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : t('error'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Modal
      isOpen
      onClose={onClose}
      title={isEdit ? t('volumes.editTitle') : t('volumes.addTitle')}
      size="sm"
      error={error ?? undefined}
      footer={
        <Row gap="2" justify="flex-end" full>
          <Button variant="ghost" onClick={onClose} disabled={isSaving}>
            {t('volumes.cancel')}
          </Button>
          <Button
            variant="primary"
            onClick={handleSubmit}
            isLoading={isSaving}
            disabled={!canSubmit}
            icon={isEdit ? <Save size={14} /> : <Plus size={14} />}
          >
            {isEdit ? t('volumes.saveChanges') : t('volumes.create')}
          </Button>
        </Row>
      }
    >
      <Stack gap="3">
        <Input
          label={t('volumes.name')}
          hint={t('volumes.nameHint')}
          value={form.name}
          onChange={e => setForm(p => ({ ...p, name: e.target.value }))}
          placeholder={t('volumes.namePlaceholder')}
          autoFocus
          required
        />
        <Stack gap="1">
          <SelectInput
            label={t('volumes.type')}
            value={form.type}
            onChange={v => setForm(p => ({ ...p, type: v as VolumeType, source: '' }))}
            options={typeOptions}
            disabled={isEdit}
          />
          <Label variant="secondary" size="sm">
            {t(`volumes.typeDescriptions.${volumeTypeKey[form.type]}`)}
          </Label>
        </Stack>
        {needsSource && (
          <Input
            label={
              form.type === 'HostPath'
                ? t('volumes.sourceLabels.hostPath')
                : t('volumes.sourceLabels.named')
            }
            value={form.source ?? ''}
            onChange={e => setForm(p => ({ ...p, source: e.target.value }))}
            placeholder={form.type === 'HostPath' ? '/srv/data' : 'my-volume'}
            required
          />
        )}
        <Input
          label={t('volumes.targetLabel')}
          value={form.target}
          onChange={e => setForm(p => ({ ...p, target: e.target.value }))}
          placeholder={t('volumes.targetPlaceholder')}
          required
        />
        <Stack gap="2">
          <Label variant="primary" size="sm" weight="semibold">
            {t('volumes.options')}
          </Label>
          <Checkbox
            label={t('volumes.readOnlyLabel')}
            description={t('volumes.readOnlyDescription')}
            checked={form.readOnly}
            onChange={e => setForm(p => ({ ...p, readOnly: e.target.checked }))}
          />
          {form.type === 'Managed' && (
            <Checkbox
              label={t('volumes.backupLabel')}
              description={t('volumes.backupDescription')}
              checked={form.backupEnabled}
              onChange={e => setForm(p => ({ ...p, backupEnabled: e.target.checked }))}
            />
          )}
        </Stack>
      </Stack>
    </Modal>
  );
}
