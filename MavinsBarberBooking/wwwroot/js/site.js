// reveal animation for elements with the class "reveal-element"
document.addEventListener("DOMContentLoaded", function () {
    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (entry.isIntersecting) {
                entry.target.classList.add("active");
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.15 });

    const revealElements = document.querySelectorAll(".reveal-element");
    revealElements.forEach((el) => observer.observe(el));
});
// end







// --- Global Loader Logic ---
document.addEventListener("DOMContentLoaded", function () {
    const loader = document.getElementById('global-loader');

    if (!loader) return;

    // 1. Always ensure loader is hidden when the page first loads or is refreshed
    loader.classList.add('d-none');
    loader.classList.remove('show-loader');

    // 2. Hide loader if user clicks the browser's "Back" button
    window.addEventListener('pageshow', function (event) {
        if (event.persisted) {
            loader.classList.add('d-none');
            loader.classList.remove('show-loader');
        }
    });

    // 3. Show loader ONLY when submitting any form (e.g., Login, Register)
    $('form').on('submit', function () {
        let isValid = true;

        // Check if form is valid using jQuery validation
        if ($(this).data('validator')) {
            isValid = $(this).valid();
        }

        // Only show the loader if the form passes validation
        if (isValid) {
            loader.classList.remove('d-none');
            // Adding a tiny timeout allows the browser to render the display change
            setTimeout(() => {
                loader.classList.add('show-loader');
            }, 10);
        }
    });
});
// --- End of Global Loader Logic ---









// --- Global Snackbar Logic ---
document.addEventListener("DOMContentLoaded", function () {
    const snackbar = document.getElementById('global-snackbar');

    if (!snackbar) return;

    // Function to initialize and display the snackbar
    function initializeSnackbar() {
        // Show the snackbar with fade-in animation
        snackbar.classList.add('show-snackbar');

        // After 4 seconds, trigger fade-out animation
        const fadeOutTimer = setTimeout(() => {
            snackbar.classList.add('fade-out');
        }, 4000);

        // After fade-out completes (4.4s total), remove the snackbar from DOM
        const removeTimer = setTimeout(() => {
            snackbar.remove();
        }, 4400);

        // Store timers for potential cleanup if needed
        snackbar.dataset.fadeOutTimer = fadeOutTimer;
        snackbar.dataset.removeTimer = removeTimer;
    }

    // Initialize the snackbar immediately on page load
    initializeSnackbar();
});
// --- End of Global Snackbar Logic ---