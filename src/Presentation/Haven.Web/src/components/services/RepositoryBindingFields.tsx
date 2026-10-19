import { useTranslation } from 'react-i18next';

import { useBranchAutocomplete } from '../../hooks/useBranchAutocomplete';
import { useRepositoryAutocomplete } from '../../hooks/useRepositoryAutocomplete';
import { BranchInput } from '../ui/BranchInput';
import { FormGroup, FormInput, FormLabel } from '../ui/Form';
import { RepositoryInput } from '../ui/RepositoryInput';
import { SelectInput } from '../ui/SelectInput';

interface Credential {
  id: string;
  displayName: string;
}

interface RepositoryBindingFieldsProps {
  gitCredentialId: string | undefined;
  onGitCredentialIdChange: (value: string | undefined) => void;
  credentials: Credential[];
  repository: string;
  onRepositoryChange: (value: string) => void;
  branch: string;
  onBranchChange: (value: string) => void;
  filePath: string;
  onFilePathChange: (value: string) => void;
  disabled?: boolean;
}

export function RepositoryBindingFields({
  gitCredentialId,
  onGitCredentialIdChange,
  credentials,
  repository,
  onRepositoryChange,
  branch,
  onBranchChange,
  filePath,
  onFilePathChange,
  disabled,
}: RepositoryBindingFieldsProps) {
  const { t } = useTranslation('services');

  const { branches, isLoading: branchesLoading } = useBranchAutocomplete(
    repository,
    gitCredentialId
  );
  const { repositories, isLoading: repositoriesLoading } =
    useRepositoryAutocomplete(gitCredentialId);

  return (
    <>
      <FormGroup>
        <SelectInput
          label={t('createPage.gitCredential')}
          value={gitCredentialId ?? ''}
          onChange={v => onGitCredentialIdChange(v || undefined)}
          options={credentials.map(c => ({ value: c.id, label: c.displayName }))}
          placeholder={t('createPage.gitCredentialPlaceholder')}
          disabled={disabled}
        />
      </FormGroup>
      <FormGroup>
        <RepositoryInput
          id="bindingRepository"
          label={`${t('createPage.repositoryUrl')} ${t('createPage.required')}`}
          placeholder={t('createPage.repositoryUrlPlaceholder')}
          value={repository}
          onChange={onRepositoryChange}
          repositories={repositories}
          isLoadingRepositories={repositoriesLoading}
          disabled={disabled}
        />
      </FormGroup>
      <FormGroup>
        <BranchInput
          label={`${t('createPage.branch')} ${t('createPage.required')}`}
          value={branch}
          onChange={onBranchChange}
          branches={branches}
          isLoadingBranches={branchesLoading}
          disabled={disabled}
        />
      </FormGroup>
      <FormGroup>
        <FormLabel htmlFor="manifestFilePath" required>
          {t('createPage.manifestFilePath')}
        </FormLabel>
        <FormInput
          id="manifestFilePath"
          type="text"
          placeholder={t('createPage.manifestFilePathPlaceholder')}
          value={filePath}
          onChange={e => onFilePathChange(e.target.value)}
          disabled={disabled}
        />
      </FormGroup>
    </>
  );
}
