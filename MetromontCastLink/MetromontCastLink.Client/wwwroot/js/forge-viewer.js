// Forge Viewer Integration for Metromont CastLink
window.ForgeViewer = {
    viewer: null,
    viewerDocument: null,
    dotNetHelper: null,
    selectedElements: [],
    selectionMode: 'single',

    // Initialize the Forge viewer
    initialize: async function (containerId, modelUrn, accessToken, dotNetReference) {
        this.dotNetHelper = dotNetReference;

        const options = {
            env: 'AutodeskProduction',
            api: 'derivativeV2',
            getAccessToken: (callback) => {
                callback(accessToken, 3600);
            }
        };

        return new Promise((resolve, reject) => {
            Autodesk.Viewing.Initializer(options, () => {
                const container = document.getElementById(containerId);

                // Create viewer
                this.viewer = new Autodesk.Viewing.GuiViewer3D(container, {
                    extensions: [
                        'Autodesk.DocumentBrowser',
                        'Autodesk.Viewing.MarkupsCore',
                        'Autodesk.Measure'
                    ]
                });

                // Start the viewer
                const startedCode = this.viewer.start();
                if (startedCode > 0) {
                    reject('Failed to start viewer');
                    return;
                }

                // Load the model
                Autodesk.Viewing.Document.load(
                    `urn:${modelUrn}`,
                    (doc) => this.onDocumentLoadSuccess(doc, resolve),
                    (error) => this.onDocumentLoadFailure(error, reject)
                );

                // Set up event listeners
                this.setupEventListeners();
            });
        });
    },

    // Document load success handler
    onDocumentLoadSuccess: function (doc, resolve) {
        this.viewerDocument = doc;

        // Get the default geometry
        const defaultModel = doc.getRoot().getDefaultGeometry();

        // Load the model
        this.viewer.loadDocumentNode(doc, defaultModel).then(() => {
            console.log('Model loaded successfully');

            // Set up selection handling
            this.setupSelectionHandling();

            // Set default view
            this.viewer.setTheme('light-theme');
            this.viewer.setQualityLevel(true, true);

            resolve();
        });
    },

    // Document load failure handler
    onDocumentLoadFailure: function (error, reject) {
        console.error('Failed to load document:', error);
        reject(error);
    },

    // Set up event listeners
    setupEventListeners: function () {
        // Selection changed event
        this.viewer.addEventListener(
            Autodesk.Viewing.SELECTION_CHANGED_EVENT,
            this.onSelectionChanged.bind(this)
        );

        // Model loaded event
        this.viewer.addEventListener(
            Autodesk.Viewing.GEOMETRY_LOADED_EVENT,
            this.onGeometryLoaded.bind(this)
        );

        // Object tree created event
        this.viewer.addEventListener(
            Autodesk.Viewing.OBJECT_TREE_CREATED_EVENT,
            this.onObjectTreeCreated.bind(this)
        );
    },

    // Set up selection handling
    setupSelectionHandling: function () {
        const selectionExtension = this.viewer.getExtension('Autodesk.Viewing.SelectionManager');
        if (selectionExtension) {
            selectionExtension.setSelectionMode(Autodesk.Viewing.SelectionMode.LEAF_OBJECT);
        }
    },

    // Selection changed handler
    onSelectionChanged: function (event) {
        const dbIds = event.dbIdArray;

        if (dbIds && dbIds.length > 0) {
            // Get properties for selected elements
            dbIds.forEach(dbId => {
                this.viewer.model.getProperties(dbId, (props) => {
                    const elementData = this.extractElementData(props);

                    // Send to Blazor component
                    if (this.dotNetHelper) {
                        this.dotNetHelper.invokeMethodAsync('OnElementSelected', JSON.stringify(elementData));
                    }
                });
            });
        }
    },

    // Extract element data from properties
    extractElementData: function (props) {
        const data = {
            Id: props.dbId,
            ExternalId: props.externalId,
            Name: props.name,
            Category: '',
            Properties: {}
        };

        // Extract relevant properties
        props.properties.forEach(prop => {
            if (prop.displayCategory === 'Identity Data' ||
                prop.displayCategory === 'Dimensions' ||
                prop.displayCategory === 'Structural' ||
                prop.displayCategory === 'Materials and Finishes') {

                if (prop.displayCategory === 'Identity Data' && prop.displayName === 'Category') {
                    data.Category = prop.displayValue;
                }

                data.Properties[prop.displayName] = prop.displayValue;
            }
        });

        // Extract geometry data
        const bbox = this.viewer.model.getBoundingBox();
        if (bbox) {
            data.Properties['BoundingBox'] = {
                min: bbox.min,
                max: bbox.max
            };
        }

        // Get volume if available
        this.getElementVolume(props.dbId).then(volume => {
            if (volume) {
                data.Properties['Volume'] = volume;
            }
        });

        return data;
    },

    // Get element volume
    getElementVolume: async function (dbId) {
        return new Promise((resolve) => {
            this.viewer.model.getProperties(dbId, (props) => {
                const volumeProp = props.properties.find(p =>
                    p.displayName === 'Volume' || p.attributeName === 'Volume'
                );
                resolve(volumeProp ? parseFloat(volumeProp.displayValue) : null);
            });
        });
    },

    // Geometry loaded handler
    onGeometryLoaded: function () {
        console.log('Geometry loaded');
        this.viewer.fitToView();
    },

    // Object tree created handler
    onObjectTreeCreated: function () {
        console.log('Object tree created');
    },

    // Enable selection mode
    enableSelection: function (mode) {
        this.selectionMode = mode;

        if (mode === 'multiple') {
            this.viewer.setSelectionMode(Autodesk.Viewing.SelectionMode.MIXED);
        } else {
            this.viewer.setSelectionMode(Autodesk.Viewing.SelectionMode.LEAF_OBJECT);
        }
    },

    // Filter elements by category
    filterByCategory: function (category) {
        const tree = this.viewer.model.getInstanceTree();
        const dbIds = [];

        tree.enumNodeChildren(tree.getRootId(), (dbId) => {
            this.viewer.model.getProperties(dbId, (props) => {
                const categoryProp = props.properties.find(p =>
                    p.displayName === 'Category' && p.displayValue.includes(category)
                );

                if (categoryProp) {
                    dbIds.push(dbId);
                }
            });
        }, true);

        // Isolate filtered elements
        if (dbIds.length > 0) {
            this.viewer.isolate(dbIds);
        }
    },

    // Get properties for elements
    getElementProperties: function (dbIds) {
        const properties = [];

        return Promise.all(dbIds.map(dbId =>
            new Promise((resolve) => {
                this.viewer.model.getProperties(dbId, (props) => {
                    properties.push(this.extractElementData(props));
                    resolve();
                });
            })
        )).then(() => properties);
    },

    // Viewer control methods
    resetView: function () {
        this.viewer.navigation.setRequestHomeView(true);
        this.viewer.fitToView();
    },

    toggleExplode: function () {
        const explodeExtension = this.viewer.getExtension('Autodesk.Explode');
        if (explodeExtension) {
            const scale = explodeExtension.getExplodeScale();
            explodeExtension.setExplodeScale(scale === 0 ? 0.5 : 0);
        }
    },

    toggleSection: function () {
        const sectionExtension = this.viewer.getExtension('Autodesk.Section');
        if (sectionExtension) {
            sectionExtension.toggleSection();
        }
    },

    toggleMeasure: function () {
        const measureExtension = this.viewer.getExtension('Autodesk.Measure');
        if (measureExtension) {
            measureExtension.activate('distance');
        }
    },

    showProperties: function () {
        const propertyPanel = this.viewer.getPropertyPanel();
        if (propertyPanel) {
            propertyPanel.setVisible(!propertyPanel.isVisible());
        }
    },

    // Isolate elements
    isolateElements: function (dbIds) {
        this.viewer.isolate(dbIds);
    },

    // Highlight elements
    highlightElements: function (dbIds, color) {
        const material = new THREE.MeshPhongMaterial({
            color: color || 0xff0000,
            opacity: 0.5,
            transparent: true
        });

        dbIds.forEach(dbId => {
            this.viewer.impl.highlightObjectNode(this.viewer.model, dbId, true, material);
        });
    },

    // Clear selection
    clearSelection: function () {
        this.viewer.clearSelection();
        this.selectedElements = [];
    },

    // Get model metadata
    getModelMetadata: function () {
        const metadata = this.viewerDocument.getRoot();
        return {
            name: metadata.name,
            guid: metadata.guid,
            role: metadata.role,
            properties: metadata.properties
        };
    },

    // Extract BIM data for calculations
    extractBIMDataForCalculation: async function (elementIds) {
        const data = {
            elements: [],
            totalVolume: 0,
            totalArea: 0,
            materials: {},
            loads: {}
        };

        for (const dbId of elementIds) {
            await new Promise((resolve) => {
                this.viewer.model.getProperties(dbId, (props) => {
                    const elementData = {
                        id: dbId,
                        name: props.name,
                        properties: {}
                    };

                    // Extract calculation-relevant properties
                    props.properties.forEach(prop => {
                        const name = prop.displayName;
                        const value = prop.displayValue;

                        // Volume and area
                        if (name === 'Volume') {
                            const volume = parseFloat(value);
                            elementData.properties.volume = volume;
                            data.totalVolume += volume;
                        } else if (name === 'Area') {
                            const area = parseFloat(value);
                            elementData.properties.area = area;
                            data.totalArea += area;
                        }

                        // Dimensions
                        else if (name === 'Length' || name === 'Width' || name === 'Height' ||
                            name === 'Thickness' || name === 'Depth') {
                            elementData.properties[name.toLowerCase()] = parseFloat(value);
                        }

                        // Material
                        else if (name === 'Material' || name === 'Structural Material') {
                            elementData.properties.material = value;
                            data.materials[value] = (data.materials[value] || 0) + 1;
                        }

                        // Structural properties
                        else if (name === 'Structural Usage' || name === 'Load Bearing') {
                            elementData.properties.structuralUsage = value;
                        }

                        // Concrete properties
                        else if (name.includes('Concrete') || name.includes('Strength')) {
                            elementData.properties[name.replace(/\s+/g, '_').toLowerCase()] = value;
                        }

                        // Reinforcement
                        else if (name.includes('Rebar') || name.includes('Reinforcement')) {
                            elementData.properties[name.replace(/\s+/g, '_').toLowerCase()] = value;
                        }
                    });

                    data.elements.push(elementData);
                    resolve();
                });
            });
        }

        // Calculate loads based on material densities
        data.loads = this.calculateLoads(data);

        return data;
    },

    // Calculate loads from BIM data
    calculateLoads: function (data) {
        const materialDensities = {
            'Concrete': 150, // PCF
            'Concrete, Precast': 150,
            'Concrete, Lightweight': 110,
            'Steel': 490,
            'Wood': 35,
            'Masonry': 125
        };

        let totalDeadLoad = 0;

        data.elements.forEach(element => {
            const volume = element.properties.volume || 0;
            const material = element.properties.material || 'Concrete';

            // Find matching density
            let density = 150; // Default to concrete
            for (const [mat, dens] of Object.entries(materialDensities)) {
                if (material.includes(mat)) {
                    density = dens;
                    break;
                }
            }

            const weight = volume * density;
            totalDeadLoad += weight;

            element.properties.weight = weight;
            element.properties.density = density;
        });

        return {
            deadLoad: totalDeadLoad,
            liveLoad: 0, // To be specified by user
            windLoad: 0, // To be specified by user
            seismicLoad: 0 // To be specified by user
        };
    },

    // Get Revit parameters mapping
    getRevitParameterMapping: function () {
        return {
            // Dimensions
            'Length': ['Length', 'L', 'Span'],
            'Width': ['Width', 'W', 'b'],
            'Height': ['Height', 'H', 'h'],
            'Depth': ['Depth', 'D', 'd'],
            'Thickness': ['Thickness', 't', 'Thk'],

            // Structural
            'ConcreteStrength': ['f\'c', 'Concrete Strength', 'fc'],
            'RebarYield': ['fy', 'Rebar Yield', 'Yield Strength'],
            'PrestressForce': ['P', 'Prestress', 'Prestressing Force'],

            // Loads
            'DeadLoad': ['DL', 'Dead Load', 'D'],
            'LiveLoad': ['LL', 'Live Load', 'L'],
            'WindLoad': ['WL', 'Wind Load', 'W'],

            // Material
            'Material': ['Material', 'Structural Material', 'Mat'],
            'Weight': ['Weight', 'Unit Weight', 'w']
        };
    },

    // Clean up
    dispose: function () {
        if (this.viewer) {
            this.viewer.finish();
            this.viewer = null;
        }
        this.viewerDocument = null;
        this.dotNetHelper = null;
        this.selectedElements = [];
    }
};