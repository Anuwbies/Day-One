// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/**
 * @title Unity Payment Gateway (Full Version)
 * @dev A robust "Cash Register" for Unity IAP.
 * Features: Admin controls, Emergency Pause, Funds Withdrawal, Item Management.
 */
contract SimplePaymentGateway {
    
    // --- SECURITY STATE ---
    address private _owner;
    bool public paused;
    uint256 private _guard; // Reentrancy Guard (1 = unlocked, 2 = locked)

    // --- SHOP DATA ---
    struct Product {
        uint256 price;   // Price in Wei
        bool isEnabled;  // Is this product currently for sale?
        string name;     // Friendly name (e.g., "100 Diamonds")
    }

    // Mapping of Product ID => Product Details
    mapping(uint256 => Product) public products;

    // --- EVENTS (Unity listens to these) ---
    event PurchaseConfirmed(address indexed buyer, uint256 productId, uint256 amountPaid);
    event ProductUpdated(uint256 productId, uint256 price, bool isEnabled, string name);
    event FundsWithdrawn(address indexed owner, uint256 amount);
    event OwnershipTransferred(address indexed previousOwner, address indexed newOwner);

    // --- CONSTRUCTOR ---
    constructor() {
        _owner = msg.sender;
        _guard = 1;
        emit OwnershipTransferred(address(0), msg.sender);
    }

    // --- MODIFIERS (Security Checks) ---
    modifier onlyOwner() {
        require(msg.sender == _owner, "Caller is not the owner");
        _;
    }

    modifier whenNotPaused() {
        require(!paused, "Shop is currently paused");
        _;
    }

    modifier nonReentrant() {
        require(_guard != 2, "ReentrancyGuard: reentrant call");
        _guard = 2;
        _;
        _guard = 1;
    }

    // ==========================================
    // USER FUNCTION (Called from Unity)
    // ==========================================

    /**
     * @notice Pay ETH to buy a product.
     * @param productId The ID of the product defined in the contract.
     */
    function purchaseProduct(uint256 productId) external payable whenNotPaused nonReentrant {
        Product memory product = products[productId];

        // Validations
        require(product.price > 0, "Product does not exist");
        require(product.isEnabled, "Product is currently disabled");
        require(msg.value >= product.price, "Insufficient ETH sent");

        // Refund excess ETH if they sent too much
        if (msg.value > product.price) {
            uint256 refundAmount = msg.value - product.price;
            (bool success, ) = payable(msg.sender).call{value: refundAmount}("");
            require(success, "Refund failed");
        }

        // Emit the receipt. Unity triggers the reward when it sees this.
        emit PurchaseConfirmed(msg.sender, productId, product.price);
    }

    // ==========================================
    // ADMIN FUNCTIONS (Manage your Shop)
    // ==========================================

    /**
     * @notice Create a new product or fully overwrite an existing one.
     * @param id The unique ID (e.g., 0 = 100 Gems)
     * @param priceInWei Price in Wei (1 ETH = 10^18 Wei)
     * @param isEnabled True to allow sales immediately
     * @param name Label for the product
     */
    function setProduct(uint256 id, uint256 priceInWei, bool isEnabled, string calldata name) external onlyOwner {
        require(priceInWei > 0, "Price cannot be zero");
        products[id] = Product(priceInWei, isEnabled, name);
        emit ProductUpdated(id, priceInWei, isEnabled, name);
    }

    /**
     * @notice Update just the price of an existing product.
     */
    function updatePrice(uint256 id, uint256 newPrice) external onlyOwner {
        require(products[id].price > 0, "Product does not exist");
        require(newPrice > 0, "Price cannot be zero");
        
        products[id].price = newPrice;
        emit ProductUpdated(id, newPrice, products[id].isEnabled, products[id].name);
    }

    /**
     * @notice Enable or Disable a product quickly (e.g. Turn off a bundle).
     */
    function setAvailability(uint256 id, bool isEnabled) external onlyOwner {
        require(products[id].price > 0, "Product does not exist");
        products[id].isEnabled = isEnabled;
        emit ProductUpdated(id, products[id].price, isEnabled, products[id].name);
    }

    /**
     * @notice Update the friendly name of a product.
     */
    function updateName(uint256 id, string calldata newName) external onlyOwner {
        require(products[id].price > 0, "Product does not exist");
        products[id].name = newName;
        emit ProductUpdated(id, products[id].price, products[id].isEnabled, newName);
    }

    /**
     * @notice Move a product to a new ID.
     */
    function changeProductId(uint256 oldId, uint256 newId) external onlyOwner {
        Product memory oldProduct = products[oldId];
        require(oldProduct.price > 0, "Old product does not exist");
        require(products[newId].price == 0, "New ID already taken");

        // Copy to new ID
        products[newId] = oldProduct;
        emit ProductUpdated(newId, oldProduct.price, oldProduct.isEnabled, oldProduct.name);

        // Delete old ID
        delete products[oldId];
        emit ProductUpdated(oldId, 0, false, "DELETED");
    }

    /**
     * @notice Pause all sales (Maintenance Mode).
     */
    function setPaused(bool _paused) external onlyOwner {
        paused = _paused;
    }

    /**
     * @notice Cash out your earnings to your wallet.
     */
    function withdraw() external onlyOwner nonReentrant {
        uint256 balance = address(this).balance;
        require(balance > 0, "No funds");
        
        (bool success, ) = payable(_owner).call{value: balance}("");
        require(success, "Withdraw failed");
        
        emit FundsWithdrawn(_owner, balance);
    }
    
    // --- HELPERS ---

    function getProductInfo(uint256 id) external view returns (uint256 price, bool isEnabled, string memory name) {
        Product memory p = products[id];
        return (p.price, p.isEnabled, p.name);
    }

    function owner() external view returns (address) {
        return _owner;
    }
}