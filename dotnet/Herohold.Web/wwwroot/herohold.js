// Browser-only presentation: theme, native dialogs, focus, and popup placement.
// Character calculations, inventory, and persistence are C# responsibilities.
window.heroholdUi = (() => {
    const preference = window.matchMedia('(prefers-color-scheme: dark)');
    let mode = 'system';

    function applyTheme(value) {
        mode = ['system', 'light', 'dark'].includes(value) ? value : 'system';
        document.documentElement.dataset.theme = mode === 'system' ? (preference.matches ? 'dark' : 'light') : mode;
        try { localStorage.setItem('folio.theme', mode); } catch { /* Appearance still works when storage is unavailable. */ }
        return mode;
    }
    function initializeTheme() {
        let saved = 'system';
        try { saved = localStorage.getItem('folio.theme') || 'system'; } catch { }
        return applyTheme(saved);
    }
    preference.addEventListener('change', () => { if (mode === 'system') applyTheme(mode); });
    let headerObserver;
    function observeHeader() {
        const header = document.getElementById('character-context');
        if (!header || header.dataset.observed) return;
        header.dataset.observed = 'true';
        headerObserver?.disconnect();
        headerObserver = new ResizeObserver(() => document.documentElement.style.setProperty('--character-header-height', header.getBoundingClientRect().height + 'px'));
        headerObserver.observe(header);
    }
    new MutationObserver(observeHeader).observe(document.documentElement, { childList: true, subtree: true });
    function openDialog(id) {
        const dialog = document.getElementById(id);
        if (!dialog || dialog.open) return;
        const returnFocus = document.activeElement;
        dialog.showModal();
        dialog.addEventListener('close', () => returnFocus?.isConnected && returnFocus.focus(), { once: true });
    }
    function closeMenus() { document.querySelectorAll(".player-menu[open]").forEach(menu => menu.open = false); document.querySelector("#app-drawer[open]")?.close(); }
    function closeDialog(id) { document.getElementById(id)?.close(); }
    function showCalculation(anchorId, popupId, reason) {
        const anchor = document.getElementById(anchorId), popup = document.getElementById(popupId);
        if (!anchor || !popup) return;
        // Closing returns focus to the trigger; that focus must not reopen the explanation.
        if (reason === 'focus' && Date.now() - Number(popup.dataset.closedAt || 0) < 500) return;
        document.querySelectorAll('.calculation-popover:popover-open').forEach(p => { if (p !== popup) p.hidePopover(); });
        if (!popup.matches(':popover-open')) popup.showPopover();
        const r = anchor.getBoundingClientRect(), gap = 8;
        popup.style.left = Math.max(gap, Math.min(r.left, innerWidth - popup.offsetWidth - gap)) + 'px';
        popup.style.top = Math.max(gap, Math.min(r.bottom + gap, innerHeight - popup.offsetHeight - gap)) + 'px';
    }
    function hideCalculation(id) { const p = document.getElementById(id); if (p?.matches(':popover-open')) { p.dataset.closedAt = Date.now(); p.hidePopover(); } }
    function download(name, mediaType, base64) { const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0)); const blob = new Blob([bytes], { type: mediaType }); const url = URL.createObjectURL(blob); const link = document.createElement('a'); link.href = url; link.download = name; document.body.appendChild(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000); }
    function selectedCharacter() { try { const current = sessionStorage.getItem('folio.selectedCharacter'); if (current) return current; } catch { } try { return localStorage.getItem('folio.selectedCharacter') || ''; } catch { return ''; } }
    function rememberCharacter(id) { try { sessionStorage.setItem('folio.selectedCharacter', id); } catch { } try { localStorage.setItem('folio.selectedCharacter', id); } catch { } }
    return { closeMenus, initializeTheme, applyTheme, openDialog, closeDialog, showCalculation, hideCalculation, download, selectedCharacter, rememberCharacter };
})();
window.heroholdUi.initializeTheme();
