function Assert-SnippyPackagedOcrIdentity($Probe, $Provenance) {
    $expected = '{0}+{1}' -f $Provenance.Version, $Provenance.Commit
    if (!$Provenance.Version -or $Provenance.Commit -notmatch '^[0-9a-f]{40}$' -or
        $Probe.Result -cne 'PASS' -or $Probe.Build -cne $expected -or
        $Probe.OcrIsolation -isnot [bool] -or !$Probe.OcrIsolation -or
        $Probe.NativeRecognition -isnot [bool] -or !$Probe.NativeRecognition -or
        $Probe.CopiesDuringBlockedOcr -ne 1 -or $Probe.EngineCreations -ne 1) {
        throw 'Packaged OCR isolation/build verification failed.'
    }
}
