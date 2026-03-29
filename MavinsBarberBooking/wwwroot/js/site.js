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




// Global Loader Logic
document.addEventListener("DOMContentLoaded", function () {
    const loader = document.getElementById('global-loader');

    // If the loader isn't on this page, do nothing
    if (!loader) return;

    // 1. Hide loader immediately when the page finishes loading
    window.addEventListener('load', function () {
        hideLoader();
    });

    // 2. Hide loader if user clicks the browser's "Back" button
    window.addEventListener('pageshow', function (event) {
        if (event.persisted) {
            hideLoader();
        }
    });

    // 3. Show loader when clicking any link
    document.querySelectorAll('a').forEach(link => {
        link.addEventListener('click', function (e) {
            const href = this.getAttribute('href');
            const target = this.getAttribute('target');

            // Only trigger for real page links (ignore # anchors or new tabs)
            if (href && !href.startsWith('#') && !href.startsWith('javascript') && target !== '_blank' && !e.ctrlKey && !e.metaKey) {
                showLoader();
            }
        });
    });

    // 4. Show loader when submitting any form (Validation-Aware!)
    $('form').on('submit', function () {
        let isValid = true;

        // Check if ASP.NET jQuery validation is attached to this specific form
        if ($(this).data('validator')) {
            // Run the validation check
            isValid = $(this).valid();
        }

        // Only show the loading screen if there are NO errors
        if (isValid) {
            showLoader();
        } else {
            // If the form has errors, forcefully keep the loader hidden
            hideLoader();
        }
    });

    // --- Helper Functions ---
    function showLoader() {
        loader.classList.remove('d-none');
    }

    function hideLoader() {
        loader.classList.add('d-none');
    }
});
// End of Global Loader Logic