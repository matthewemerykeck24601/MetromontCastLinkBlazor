// Replace the previous project-dropdown-fix.js with this enhanced version
// Save as wwwroot/js/project-dropdown-enhanced.js

window.projectDropdownEnhanced = {
    // Initialize the enhanced dropdown
    initialize: function () {
        // Wait for Blazor to render
        setTimeout(() => {
            this.setupDropdownBehavior();
            this.setupKeyboardShortcuts();
        }, 500);
    },

    // Setup enhanced dropdown behavior
    setupDropdownBehavior: function () {
        const dropdowns = document.querySelectorAll('.project-dropdown-container .e-ddl');

        dropdowns.forEach(dropdown => {
            const input = dropdown.querySelector('.e-input');

            if (input) {
                // Single click to open and focus filter
                input.addEventListener('click', (e) => {
                    e.stopPropagation();
                    input.focus();

                    // Auto-focus the filter input when dropdown opens
                    setTimeout(() => {
                        this.focusFilterInput();
                    }, 100);
                });

                // Also handle keyboard activation
                input.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        input.click();
                    }
                });
            }
        });

        // Watch for dropdown opening
        this.observeDropdownOpen();
    },

    // Auto-focus the filter input when dropdown opens
    focusFilterInput: function () {
        const filterInput = document.querySelector('.e-dropdown-popup .e-input-filter');
        if (filterInput) {
            filterInput.focus();
            filterInput.select(); // Select any existing text for easy replacement
        }
    },

    // Observe when dropdown opens to focus filter
    observeDropdownOpen: function () {
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                if (mutation.type === 'childList') {
                    const popup = document.querySelector('.e-dropdown-popup.e-popup-open');
                    if (popup) {
                        setTimeout(() => {
                            this.focusFilterInput();
                        }, 50);
                    }
                }
            });
        });

        // Observe the body for dropdown popup additions
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    },

    // Setup keyboard shortcuts for better UX
    setupKeyboardShortcuts: function () {
        document.addEventListener('keydown', (e) => {
            // Ctrl+P or Cmd+P to focus project dropdown
            if ((e.ctrlKey || e.metaKey) && e.key === 'p') {
                e.preventDefault();
                const dropdownInput = document.querySelector('.project-dropdown-container .e-input');
                if (dropdownInput) {
                    dropdownInput.click();
                    setTimeout(() => {
                        this.focusFilterInput();
                    }, 100);
                }
            }
        });
    },

    // Highlight matching text in dropdown items
    highlightSearchText: function (searchText) {
        if (!searchText) return;

        const listItems = document.querySelectorAll('.e-dropdown-popup .e-list-item');
        listItems.forEach(item => {
            const projectName = item.querySelector('.project-name');
            if (projectName) {
                const text = projectName.textContent;
                const regex = new RegExp(`(${searchText})`, 'gi');
                const highlighted = text.replace(regex, '<mark>$1</mark>');
                projectName.innerHTML = highlighted;
            }
        });
    },

    // Keep dropdown open and properly positioned
    maintainDropdownState: function () {
        const popup = document.querySelector('.e-dropdown-popup.e-popup-open');
        if (popup) {
            popup.style.display = 'block';
            popup.style.opacity = '1';
            popup.style.visibility = 'visible';
            popup.style.pointerEvents = 'auto';
            popup.style.zIndex = '10000';
        }
    }
};

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.projectDropdownEnhanced.initialize();
    });
} else {
    window.projectDropdownEnhanced.initialize();
}

// Re-initialize after Blazor navigation
if (window.Blazor) {
    window.addEventListener('blazor:after-start-up', () => {
        window.projectDropdownEnhanced.initialize();
    });
}