import { FileCheck2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiFetchBlobUrl } from '../services/apiClient';

interface ProtectedAssetPreviewProps {
  assetKey: string | null;
  localPreviewUrl?: string | null;
  alt: string;
}

export function ProtectedAssetPreview({
  assetKey,
  localPreviewUrl,
  alt,
}: ProtectedAssetPreviewProps) {
  const { t } = useTranslation();
  const [loadedAsset, setLoadedAsset] = useState<{ key: string; url: string } | null>(null);

  useEffect(() => {
    if (localPreviewUrl || !assetKey || assetKey.startsWith('pending-')) {
      return;
    }

    const targetKey = assetKey;
    let revoked = false;
    let objectUrl: string | null = null;
    async function load() {
      try {
        const url = await apiFetchBlobUrl(`onboarding/assets/${encodeURIComponent(targetKey)}`);
        if (!revoked) {
          objectUrl = url;
          setLoadedAsset({ key: targetKey, url });
        } else {
          URL.revokeObjectURL(url);
        }
      } catch {
        // Unauthenticated or unreachable asset falls back cleanly to metadata pill
      }
    }
    void load();

    return () => {
      revoked = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [assetKey, localPreviewUrl]);

  if (!assetKey) return null;

  const src =
    localPreviewUrl || (loadedAsset && loadedAsset.key === assetKey ? loadedAsset.url : null);

  return (
    <div className="mt-2 flex items-center gap-2.5 rounded-lg border border-brand-200 bg-brand-50/70 px-2.5 py-1.5 text-[11px] text-brand-900">
      {src ? (
        <img
          src={src}
          alt={alt}
          className="h-10 w-16 shrink-0 rounded border border-brand-200 bg-white object-contain p-0.5"
        />
      ) : (
        <FileCheck2 className="h-4 w-4 shrink-0 text-brand-700" />
      )}
      <div className="min-w-0 flex-1">
        <p className="truncate font-semibold">
          {assetKey.startsWith('pending-') ? t('onboarding.previewPendingLocal') : t('onboarding.uploadedReady')}
        </p>
        <p className="truncate font-mono text-[10px] text-brand-700">{assetKey}</p>
      </div>
    </div>
  );
}
