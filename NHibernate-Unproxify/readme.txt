Removes proxies from a complex object, to prepare it for serialization. 

NHibernate uses runtime proxies to support lazy loading, which is the default configuration. However, proxy types pose several problems to serialization.

There are alternative strategies to avoid this problem:
1. Use specific DTOs and copy to DTO only fully initialized properties. 
This is the best solution, but is also complex, as it requires both managing DTOs and mappings/projections which will ignore possibly unitialized properties.
2. Eager load everything.
Fits very simple schemas and small databases. Otherwise will load an unreasonable amount of data.
3. Turn off proxy creation
I have not been able to do so, in spite of the documentation. Proxies are created even when lazy loading is off. 
4. Create a custom serializer to handle proxies
This is both complex and usually signals a bad design, as the proxies have no use outside of the original process boundaries. 
5. Create custom proxies which are both serializable and work across process boundaries
This is very complex, however examples exist. This is the solution for the scenario when you want client-server lazy loading. 

Another strategy is to query the objects as usual, and strip any uninitialized proxies before serialization. Uninitialized collections and references will be replaced with nulls.
This approach has several advantages:

1. The objects are immediately ready for serialization.
2. Does not eager-load any related objects which you did not fetched explicitely.
3. Does not require updating client-server contracts and introducing DTOs (altough, in the longer run, DTOs are the better solution).

The provided solution implements this approach in two ways:
1. An extension method to IQueryable<T> called SelectWithoutProxies
2. A ValueInjection strategy (to be used with the excellent ValueInjecter) called NHUnproxifyInjection

Usage:

Using the extention method:

	IList<Product> res = session.Query<Product>()
									.Where( p => p.Users > 100000 )
									.SelectWithoutProxies()
									.ToList();
						

Using the extension method and converting to DTOs:

	IList<ProductDTO> res = session.Query<Product>()
									.Where( p => p.Users > 100000 )
									.SelectWithoutProxies<Product, ProductDTO>()
									.ToList();

Using the ValueInjection:

	var productDTO = new ProductDTO();
	productDTO.InjectFrom( new NHUnproxifyInjection(), some_product );



Additional properties of the solution:
1. Handles complex objects of any depth.
2. Handles circular references. (Note: If using WCF, you should set the IsReferenceAttribute to true on your DataContracts. Without it, WCF will not correctly handle circular references and may be inefficient in cases where an object is referenced multiple times)
3. Replaces un-initialized proxies with nulls.

*** NOTE ***: This is not production quality code. Bugs may exist, the internal structure and documentation may be lacking. Your comments are welcome. It gets the job done for my prototype project, and it may work well with yours. 

License: I keep the ownership and copyright, but grant you the right to use this code in any application, commercial or free, at the sole condition of dropping me a line that this code was useful to you. 

Roadmap: If this will turn out to be useful to many people, I will improve the code, document it and create unit-tests. If you found any bugs, please let me know. 

