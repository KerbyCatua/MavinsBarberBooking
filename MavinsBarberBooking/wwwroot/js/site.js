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
    // Ensure your HTML <div class="loader"> has id="global-loader"
    const loader = document.getElementById('global-loader');
    let loaderTimer;
    const MIN_DISPLAY_TIME = 1000; // 1 second minimum

    if (!loader) return;

    // 1. Check if we should still be showing a loader from the previous page
    const storedStartTime = sessionStorage.getItem('loaderStartTime');
    if (storedStartTime) {
        const elapsedTime = Date.now() - parseInt(storedStartTime);

        if (elapsedTime < MIN_DISPLAY_TIME) {
            loader.classList.remove('d-none');
            loader.classList.add('show-loader');

            setTimeout(() => {
                hideLoader();
            }, MIN_DISPLAY_TIME - elapsedTime);
        } else {
            sessionStorage.removeItem('loaderStartTime');
            hideLoader();
        }
    } else {
        hideLoader();
    }

    // 2. Hide loader if user clicks the browser's "Back" button
    window.addEventListener('pageshow', function (event) {
        if (event.persisted) {
            sessionStorage.removeItem('loaderStartTime');
            hideLoader();
        }
    });

    // 3. Show loader when clicking any link
    document.querySelectorAll('a').forEach(link => {
        link.addEventListener('click', function (e) {
            const href = this.getAttribute('href');
            const target = this.getAttribute('target');

            if (href && !href.startsWith('#') && !href.startsWith('javascript') && target !== '_blank' && !e.ctrlKey && !e.metaKey) {
                showLoader();
            }
        });
    });

    // 4. Show loader when submitting any form (using jQuery as per your original)
    if (window.jQuery) {
        $('form').on('submit', function () {
            let isValid = true;
            if ($(this).data('validator')) {
                isValid = $(this).valid();
            }

            if (isValid) {
                showLoader();
            } else {
                hideLoader();
            }
        });
    }

    // --- Helper Functions ---

    function showLoader() {
        clearTimeout(loaderTimer);
        loader.classList.remove('d-none');

        loaderTimer = setTimeout(() => {
            loader.classList.add('show-loader');
            sessionStorage.setItem('loaderStartTime', Date.now().toString());
        }, 300);
    }

    function hideLoader() {
        clearTimeout(loaderTimer);

        if (!loader.classList.contains('show-loader')) {
            loader.classList.add('d-none');
            return;
        }

        sessionStorage.removeItem('loaderStartTime');
        loader.classList.remove('show-loader');

        setTimeout(() => {
            loader.classList.add('d-none');
        }, 300);
    }
});
// End of Global Loader Logic