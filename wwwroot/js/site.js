// Auto-dismiss TempData alerts after 4 seconds
window.addEventListener('DOMContentLoaded', () => {
    const alert = document.getElementById('tempAlert');
    if (alert) {
        setTimeout(() => {
            alert.style.transition = 'opacity 0.5s ease';
            alert.style.opacity = '0';
            setTimeout(() => alert.remove(), 500);
        }, 4000);
    }

    // Status select color sync
    document.querySelectorAll('.status-select').forEach(sel => {
        updateSelectColor(sel);
        sel.addEventListener('change', () => updateSelectColor(sel));
    });
});

function updateSelectColor(select) {
    select.className = select.className.replace(/status-\w+/g, '');
    const val = select.value.toLowerCase();
    select.classList.add('status-select', `status-${val}`);
}
