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




// START LOADER LOGIC
document.addEventListener("DOMContentLoaded", function () {
    const loader = document.getElementById('global-loader');
    let loaderTimer;
    const MIN_DISPLAY_TIME = 1000;

    if (!loader) return;

    const storedStartTime = sessionStorage.getItem('loaderStartTime');
    if (storedStartTime) {
        const elapsedTime = Date.now() - parseInt(storedStartTime);
        if (elapsedTime < MIN_DISPLAY_TIME) {
            // Instant show if navigating
            loader.classList.remove('d-none');
            // Small delay to ensure browser registers the removal of d-none before starting transition
            setTimeout(() => {
                loader.classList.add('show-loader');
                document.body.classList.add('loader-active');
            }, 10);

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

    window.addEventListener('pageshow', function (event) {
        if (event.persisted) {
            sessionStorage.removeItem('loaderStartTime');
            hideLoader();
        }
    });

    document.querySelectorAll('a').forEach(link => {
        link.addEventListener('click', function (e) {
            const href = this.getAttribute('href');
            const target = this.getAttribute('target');
            if (href && !href.startsWith('#') && !href.startsWith('javascript') && target !== '_blank' && !e.ctrlKey && !e.metaKey) {
                showLoader();
            }
        });
    });

    if (window.jQuery) {
        $('form').on('submit', function () {
            let isValid = true;
            if ($(this).data('validator')) { isValid = $(this).valid(); }
            if (isValid) { showLoader(); } else { hideLoader(); }
        });
    }

    function showLoader() {
        clearTimeout(loaderTimer);
        loader.classList.remove('d-none');
        document.body.classList.add('loader-active');

        // Delay the opacity class slightly so the 'd-none' removal is processed first
        loaderTimer = setTimeout(() => {
            loader.classList.add('show-loader');
            sessionStorage.setItem('loaderStartTime', Date.now().toString());
        }, 400);
    }

    function hideLoader() {
        clearTimeout(loaderTimer);

        if (!loader.classList.contains('show-loader')) {
            loader.classList.add('d-none');
            document.body.classList.remove('loader-active');
            return;
        }

        sessionStorage.removeItem('loaderStartTime');
        loader.classList.remove('show-loader');
        document.body.classList.remove('loader-active');

        // Wait for CSS transition (0.4s) to finish before adding d-none
        setTimeout(() => {
            if (!loader.classList.contains('show-loader')) {
                loader.classList.add('d-none');
            }
        }, 400);
    }
});
// END LOADER LOGIC