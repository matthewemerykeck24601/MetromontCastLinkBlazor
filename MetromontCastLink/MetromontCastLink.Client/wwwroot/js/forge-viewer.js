// Forge Viewer Integration for Metromont CastLink
// Complete rewrite with proper initialization and error handling

window.ForgeViewer = {
    viewer: null,
    viewerDocument: null,
    dotNetHelper: null,
    selectedElements: [],
    selectionMode: 'single',
    isInitialized: false,
    currentModelUrn: null,
    viewerContainer: null,
    currentAccessToken: null,

    // Initialize empty viewer (no model loaded)
    initializeEmpty: async function (containerId, accessToken, dotNetReference) {
        this.dotNetHelper = dotNetReference;
        this.viewerContainer = containerId;
        this.currentAccessToken = accessToken; // Store the token for later use

        const options = {
            env: 'AutodeskProduction2',  // Use Production2 for ACC/BIM360 models
            api: 'streamingV2',          // Use streamingV2 for better performance
            getAccessToken: (callback) => {
                // Use the stored token or get a fresh one if needed
                callback(this.currentAccessToken, 3600);
            }
        };

        return new Promise((resolve, reject) => {
            // Check if Autodesk namespace exists
            if (typeof Autodesk === 'undefined') {
                reject('Forge Viewer SDK not loaded');
                return;
            }

            Autodesk.Viewing.Initializer(options, () => {
                const container = document.getElementById(containerId);
                if (!container) {
                    reject('Container element not found');
                    return;
                }

                // Create viewer with ACC-like configuration
                this.viewer = new Autodesk.Viewing.GuiViewer3D(container, {
                    theme: 'light-theme',
                    disableSelection: false
                });

                // Start the viewer
                const startedCode = this.viewer.start();
                if (startedCode > 0) {
                    reject('Failed to start viewer: ' + startedCode);
                    return;
                }

                // Viewer started successfully - set initialized flag
                this.isInitialized = true;

                // Show empty viewer message
                this.showEmptyState();

                // Set up base event listeners (without model-specific ones)
                this.setupBaseEventListeners();

                resolve(true);
            });
        });
    },

    // Load a model into the initialized viewer
    loadModel: async function (modelUrn, accessToken) {
        if (!this.isInitialized || !this.viewer) {
            throw new Error('Viewer not initialized. Call initializeEmpty first.');
        }

        console.log('Loading model with URN:', modelUrn);
        console.log('Access token provided:', accessToken ? 'Yes' : 'No');

        // Clear any existing model
        if (this.viewerDocument) {
            this.viewer.unloadModel(this.viewer.model);
            this.viewerDocument = null;
        }

        this.currentModelUrn = modelUrn;

        // Update the stored access token if provided
        if (accessToken) {
            this.currentAccessToken = accessToken;
        }

        return new Promise((resolve, reject) => {
            // Hide empty state
            this.hideEmptyState();

            // Load the document with the access token callback
            const documentId = `urn:${modelUrn}`;

            console.log('Loading document:', documentId);

            Autodesk.Viewing.Document.load(
                documentId,
                (doc) => {
                    console.log('Document loaded successfully');
                    this.viewerDocument = doc;
                    this.onDocumentLoadSuccess(doc, resolve);
                },
                (errorCode, errorMsg, statusCode, statusText) => {
                    console.error('Document load error details:');
                    console.error('Error Code:', errorCode);
                    console.error('Error Message:', errorMsg);
                    console.error('Status Code:', statusCode);
                    console.error('Status Text:', statusText);

                    if (errorCode === 4 || statusCode === 401) {
                        console.error('Authentication Error (401): Check that the model URN exists and token is valid');
                    } else if (errorCode === 5) {
                        console.error('Network Error: Could not reach Autodesk servers');
                    } else if (errorCode === 6) {
                        console.error('Model Translation Error: The model may not be viewable yet');
                    }

                    this.onDocumentLoadFailure(errorCode, reject);
                },
                {
                    getAccessToken: (callback) => {
                        if (!this.currentAccessToken) {
                            console.error('No access token available!');
                            callback('', 0);
                        } else {
                            callback(this.currentAccessToken, 3600);
                        }
                    }
                }
            );
        });
    },

    // Document load success handler
    onDocumentLoadSuccess: function (doc, resolve) {
        // Get the default geometry
        const defaultModel = doc.getRoot().getDefaultGeometry();
        if (!defaultModel) {
            this.showEmptyState();
            resolve(false);
            return;
        }

        // Load the model into viewer
        this.viewer.loadDocumentNode(doc, defaultModel).then(() => {
            console.log('Model loaded successfully');

            // Wait for geometry to be fully loaded before setting up selection
            this.viewer.addEventListener(
                Autodesk.Viewing.GEOMETRY_LOADED_EVENT,
                () => {
                    // Now safe to set up selection handling
                    this.setupSelectionHandling();

                    // Set up model-specific event listeners
                    this.setupModelEventListeners();

                    // Set default view settings
                    this.viewer.setTheme('light-theme');
                    this.viewer.setQualityLevel(true, true);
                    this.viewer.setGroundShadow(false);
                    this.viewer.setGroundReflection(false);
                    this.viewer.setGhosting(true);
                    this.viewer.setEnvMapBackground(false);

                    // Fit to view
                    this.viewer.fitToView();

                    // Notify .NET that model is loaded
                    if (this.dotNetHelper) {
                        this.dotNetHelper.invokeMethodAsync('OnModelLoaded', doc.getRoot().data.name);
                    }

                    resolve(true);
                },
                { once: true }
            );
        }).catch(error => {
            console.error('Error loading model node:', error);
            this.showEmptyState();
            resolve(false);
        });
    },

    // Document load failure handler
    onDocumentLoadFailure: function (error, reject) {
        console.error('Failed to load document:', error);
        this.showEmptyState();
        reject(error);
    },

    // Show empty state message
    showEmptyState: function () {
        const container = document.getElementById(this.viewerContainer);
        if (!container) return;

        // Remove existing empty state if any
        const existing = container.querySelector('.viewer-empty-state');
        if (existing) existing.remove();

        // Create empty state UI
        const emptyState = document.createElement('div');
        emptyState.className = 'viewer-empty-state';
        emptyState.innerHTML = `
            <div class="empty-state-content">
                <svg class="empty-state-icon" width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                    <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path>
                    <polyline points="3.27 6.96 12 12.01 20.73 6.96"></polyline>
                    <line x1="12" y1="22.08" x2="12" y2="12"></line>
                </svg>
                <h3>No Model Loaded</h3>
                <p>Select a model from the dropdown above to begin</p>
            </div>
        `;
        container.appendChild(emptyState);
    },

    // Hide empty state message
    hideEmptyState: function () {
        const container = document.getElementById(this.viewerContainer);
        if (!container) return;

        const emptyState = container.querySelector('.viewer-empty-state');
        if (emptyState) {
            emptyState.remove();
        }
    },

    // Set up base event listeners (viewer-level, not model-specific)
    setupBaseEventListeners: function () {
        if (!this.viewer) return;

        // Viewer resize event
        window.addEventListener('resize', () => {
            if (this.viewer) {
                this.viewer.resize();
            }
        });

        // Escape key handler
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && this.viewer) {
                this.viewer.clearSelection();
                this.selectedElements = [];
                if (this.dotNetHelper) {
                    this.dotNetHelper.invokeMethodAsync('OnSelectionCleared');
                }
            }
        });
    },

    // Set up model-specific event listeners
    setupModelEventListeners: function () {
        if (!this.viewer || !this.viewer.model) return;

        // Selection changed event
        this.viewer.addEventListener(
            Autodesk.Viewing.SELECTION_CHANGED_EVENT,
            this.onSelectionChanged.bind(this)
        );

        // Object tree created event
        this.viewer.addEventListener(
            Autodesk.Viewing.OBJECT_TREE_CREATED_EVENT,
            this.onObjectTreeCreated.bind(this)
        );

        // Camera changed event for view state tracking
        this.viewer.addEventListener(
            Autodesk.Viewing.CAMERA_CHANGE_EVENT,
            this.onCameraChanged.bind(this)
        );
    },

    // Set up selection handling - ONLY call after model is loaded
    setupSelectionHandling: function () {
        if (!this.viewer || !this.viewer.impl || !this.viewer.impl.selector) {
            console.warn('Viewer not ready for selection setup');
            return;
        }

        try {
            // Use the viewer's selection mode directly
            this.viewer.impl.selector.setSelectionMode(Autodesk.Viewing.SelectionMode.LEAF_OBJECT);
        } catch (error) {
            console.warn('Could not set selection mode:', error);
        }
    },

    // Selection changed handler
    onSelectionChanged: function (event) {
        const dbIds = event.dbIdArray;

        if (!dbIds || dbIds.length === 0) {
            this.selectedElements = [];
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('OnSelectionCleared');
            }
            return;
        }

        // Get properties for selected elements
        const promises = dbIds.map(dbId => this.getElementProperties(dbId));

        Promise.all(promises).then(elements => {
            this.selectedElements = elements.filter(e => e !== null);

            // Send to Blazor
            if (this.dotNetHelper && this.selectedElements.length > 0) {
                if (this.selectionMode === 'single' && this.selectedElements.length > 0) {
                    this.dotNetHelper.invokeMethodAsync('OnElementSelected',
                        JSON.stringify(this.selectedElements[0]));
                } else {
                    this.dotNetHelper.invokeMethodAsync('OnMultipleElementsSelected',
                        JSON.stringify(this.selectedElements));
                }
            }
        });
    },

    // Get element properties
    getElementProperties: function (dbId) {
        return new Promise((resolve) => {
            if (!this.viewer || !this.viewer.model) {
                resolve(null);
                return;
            }

            this.viewer.model.getProperties(dbId, (props) => {
                resolve(this.extractElementData(props));
            }, (error) => {
                console.warn('Error getting properties for dbId ' + dbId, error);
                resolve(null);
            });
        });
    },

    // Extract element data from properties
    extractElementData: function (props) {
        if (!props) return null;

        const findProperty = (name) => {
            const prop = props.properties.find(p =>
                p.displayName === name || p.attributeName === name
            );
            return prop ? prop.displayValue : null;
        };

        return {
            id: props.dbId,
            externalId: props.externalId,
            name: props.name,
            category: findProperty('Category'),
            family: findProperty('Family'),
            type: findProperty('Type'),
            level: findProperty('Level'),
            mark: findProperty('Mark'),
            length: parseFloat(findProperty('Length')) || 0,
            width: parseFloat(findProperty('Width')) || 0,
            height: parseFloat(findProperty('Height')) || 0,
            volume: parseFloat(findProperty('Volume')) || 0,
            area: parseFloat(findProperty('Area')) || 0,
            weight: parseFloat(findProperty('Weight')) || 0,
            allProperties: props.properties
        };
    },

    // Object tree created handler
    onObjectTreeCreated: function () {
        console.log('Object tree created');

        // Get model statistics
        if (this.viewer && this.viewer.model) {
            const tree = this.viewer.model.getInstanceTree();
            if (tree) {
                let count = 0;
                tree.enumNodeChildren(tree.getRootId(), () => count++, true);

                if (this.dotNetHelper) {
                    this.dotNetHelper.invokeMethodAsync('OnModelStatisticsReady', count);
                }
            }
        }
    },

    // Camera changed handler
    onCameraChanged: function () {
        // Could be used to save view state
    },

    // Enable selection mode
    enableSelection: function (mode) {
        if (!this.viewer || !this.viewer.impl || !this.viewer.impl.selector) {
            console.warn('Viewer not ready for selection mode change');
            return;
        }

        this.selectionMode = mode;

        try {
            if (mode === 'multiple') {
                this.viewer.impl.selector.setSelectionMode(Autodesk.Viewing.SelectionMode.MIXED);
            } else {
                this.viewer.impl.selector.setSelectionMode(Autodesk.Viewing.SelectionMode.LEAF_OBJECT);
            }
        } catch (error) {
            console.warn('Error setting selection mode:', error);
        }
    },

    // Filter Cloud Central models only
    filterCloudModels: function (models) {
        if (!models || !Array.isArray(models)) return [];

        // Filter for Cloud Central models (not static/local copies)
        return models.filter(model => {
            // Check if it's a cloud model by looking for specific attributes
            const isCloudModel = model.attributes &&
                model.attributes.extension &&
                model.attributes.extension.type === 'items:autodesk.bim360:C4RModel';

            // Also check for Revit cloud models
            const isRevitCloud = model.attributes &&
                model.attributes.extension &&
                model.attributes.extension.type === 'items:autodesk.core:File' &&
                model.attributes.mimeType === 'application/vnd.autodesk.revit';

            return isCloudModel || isRevitCloud;
        });
    },

    // Get available views in the model
    getAvailableViews: function () {
        if (!this.viewerDocument) return [];

        const viewables = this.viewerDocument.getRoot().search({
            'type': 'geometry'
        });

        return viewables.map(view => ({
            guid: view.guid,
            name: view.name || 'Unnamed View',
            role: view.role,
            is2D: view.is2D(),
            is3D: view.is3D()
        }));
    },

    // Load a specific view
    loadView: function (viewGuid) {
        if (!this.viewerDocument || !this.viewer) return;

        const viewables = this.viewerDocument.getRoot().search({
            'guid': viewGuid
        });

        if (viewables.length > 0) {
            this.viewer.loadDocumentNode(this.viewerDocument, viewables[0]);
        }
    },

    // Viewer control methods
    resetView: function () {
        if (this.viewer) {
            this.viewer.navigation.setRequestHomeView(true);
            this.viewer.fitToView();
        }
    },

    toggleExplode: function () {
        if (!this.viewer) return;

        try {
            // Try to load and use the explode extension
            this.viewer.loadExtension('Autodesk.Explode').then((explodeExtension) => {
                const scale = explodeExtension.getExplodeScale();
                explodeExtension.setExplodeScale(scale === 0 ? 0.5 : 0);
            }).catch(err => {
                console.warn('Explode extension not available');
            });
        } catch (error) {
            console.warn('Error toggling explode:', error);
        }
    },

    toggleSection: function () {
        if (!this.viewer) return;

        try {
            // For section, use the built-in section tool
            const ext = this.viewer.getExtension('Autodesk.Section');
            if (ext) {
                ext.toggleSection();
            } else {
                console.warn('Section extension not available');
            }
        } catch (error) {
            console.warn('Error toggling section:', error);
        }
    },

    toggleMeasure: function () {
        if (!this.viewer) return;

        try {
            // Try to load and use the measure extension
            this.viewer.loadExtension('Autodesk.Measure').then((measureExtension) => {
                if (measureExtension.isActive()) {
                    measureExtension.deactivate();
                } else {
                    measureExtension.activate();
                }
            }).catch(err => {
                console.warn('Measure extension not available');
            });
        } catch (error) {
            console.warn('Error toggling measure:', error);
        }
    },

    showProperties: function () {
        if (!this.viewer) return;

        try {
            // Use the built-in property panel
            if (this.viewer.getPropertyPanel) {
                const panel = this.viewer.getPropertyPanel(true);
                panel.setVisible(!panel.isVisible());
            }
        } catch (error) {
            console.warn('Error showing properties:', error);
        }
    },

    toggleGhosting: function () {
        if (!this.viewer) return;

        try {
            // Toggle ghosting for hidden objects
            const ghosting = this.viewer.getGhosting();
            this.viewer.setGhosting(!ghosting);
        } catch (error) {
            console.warn('Error toggling ghosting:', error);
        }
    },

    showSearch: function () {
        if (!this.viewer) return;

        try {
            // Try to load search extension
            this.viewer.loadExtension('Autodesk.Search').then((searchExtension) => {
                searchExtension.showSearchWindow();
            }).catch(err => {
                console.warn('Search extension not available');
            });
        } catch (error) {
            console.warn('Error showing search:', error);
        }
    },

    // Isolate selected elements
    isolateSelected: function () {
        if (this.viewer && this.selectedElements.length > 0) {
            const dbIds = this.selectedElements.map(e => e.id);
            this.viewer.isolate(dbIds);
        }
    },

    // Hide selected elements
    hideSelected: function () {
        if (this.viewer && this.selectedElements.length > 0) {
            const dbIds = this.selectedElements.map(e => e.id);
            this.viewer.hide(dbIds);
        }
    },

    // Show all elements
    showAll: function () {
        if (this.viewer) {
            this.viewer.isolate([]);
            this.viewer.showAll();
        }
    },

    // Clear selection
    clearSelection: function () {
        if (this.viewer) {
            this.viewer.clearSelection();
            this.selectedElements = [];
        }
    },

    // Get current view state
    getViewState: function () {
        if (!this.viewer) return null;

        const viewState = this.viewer.getState();
        return JSON.stringify(viewState);
    },

    // Restore view state
    restoreViewState: function (viewStateJson) {
        if (!this.viewer) return;

        try {
            const viewState = JSON.parse(viewStateJson);
            this.viewer.restoreState(viewState);
        } catch (error) {
            console.error('Error restoring view state:', error);
        }
    },

    // Take screenshot
    takeScreenshot: function () {
        if (!this.viewer) return null;

        return this.viewer.getScreenShot(1920, 1080);
    },

    // Clean up
    destroy: function () {
        if (this.viewer) {
            this.viewer.finish();
            this.viewer = null;
        }
        this.viewerDocument = null;
        this.selectedElements = [];
        this.isInitialized = false;
        this.currentModelUrn = null;
    }
};

// Export for module usage if needed
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ForgeViewer;
}